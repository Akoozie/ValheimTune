using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using HarmonyLib;
using UnityEngine;

namespace ValheimTune.Patches
{
    [HarmonyPatch]
    public static class SavePatches
    {
        private static bool Active => Compat.ReplacementsAllowed && Cfg.SlicedSave.Value && ZNet.instance != null && ZNet.instance.IsServer();

        // Buffer produced on the main thread, consumed by SaveAsync on the writer thread.
        private static byte[] s_buffer;
        private static int s_count;
        private static bool s_inProgress;

        // 1. No clone of the field tables: alias the save tables to the live ones. Only the main
        //    thread reads them from now on (the Slices coroutine below guarantees the thread never does).
        [HarmonyPatch(typeof(ZDOExtraData), nameof(ZDOExtraData.PrepareSave))]
        [HarmonyPrefix]
        private static bool ExtraDataPrepare()
        {
            if (!Active) return true;
            ZDOExtraData.RegenerateConnectionHashData();
            ZDOExtraData.s_saveFloats = ZDOExtraData.s_floats;
            ZDOExtraData.s_saveVec3s = ZDOExtraData.s_vec3;
            ZDOExtraData.s_saveQuats = ZDOExtraData.s_quats;
            ZDOExtraData.s_saveInts = ZDOExtraData.s_ints;
            ZDOExtraData.s_saveLongs = ZDOExtraData.s_longs;
            ZDOExtraData.s_saveStrings = ZDOExtraData.s_strings;
            ZDOExtraData.s_saveByteArrays = ZDOExtraData.s_byteArrays;
            ZDOExtraData.s_saveConnections = ZDOExtraData.s_connectionsHashData;
            return false;
        }

        // 2. ZDOMan.PrepareSave is not called at all in sliced mode (SaveWorld replacement below
        //    does the equivalent). Guard it anyway so a stray caller cannot clone.
        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.PrepareSave))]
        [HarmonyPrefix]
        private static bool ZdoManPrepare() => !Active;

        // 3. Writer thread: dump the buffer. Same byte layout as vanilla SaveAsync (ZDOMan.cs:199-218).
        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.SaveAsync))]
        [HarmonyPrefix]
        private static bool SaveAsync(BinaryWriter writer)
        {
            // Keyed on the buffer, not the config flag: a reload that flips SlicedSave mid-save must
            // not hand vanilla SaveAsync a null m_saveData on the writer thread.
            if (s_buffer == null) return true;
            writer.Write(s_buffer, 0, s_buffer.Length);
            ZLog.Log("Saved " + s_count + " ZDOs (ValheimTune sliced)");
            s_buffer = null;
            ZDOExtraData.ClearSave();
            return false;
        }

        // 4. SaveWorld: run the slice coroutine, then the vanilla thread.
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.SaveWorld))]
        [HarmonyPrefix]
        private static bool SaveWorld(ZNet __instance, bool sync)
        {
            if (!Active) return true;
            if (s_inProgress) { ZLog.LogWarning("[ValheimTune] save already in progress, skipping"); return false; }
            if (sync)
            {
                // Shutdown path: no frames to slice across. Run every slice back to back.
                var e = Slices(__instance, sync: true);
                while (e.MoveNext()) { }
                return false;
            }
            Plugin.Instance.StartCoroutine(Slices(__instance, sync: false));
            return false;
        }

        private static IEnumerator Slices(ZNet net, bool sync)
        {
            s_inProgress = true;
            ZNet.WorldSaveStarted?.Invoke();     // public static Action on ZNet (src_server/ZNet.cs:111)
            if (net.m_saveThread != null && net.m_saveThread.IsAlive) { net.m_saveThread.Join(); net.m_saveThread = null; }
            net.m_saveStartTime = Time.realtimeSinceStartup;

            // Non-yielding setup: gather live persistent ZDOs and prep the writer. Safe to try/catch
            // (iterator methods cannot yield inside a try that has a catch, so nothing below yields).
            List<ZDO> zdos = null;
            ZPackage pkg = null;
            BinaryWriter bw = null;
            MemoryStream ms = null;
            bool setupOk = false;
            try
            {
                var man = ZDOMan.instance;
                zdos = new List<ZDO>(man.NrOfObjects());
                for (int i = 0; i < man.m_objectsBySector.Length; i++)
                {
                    var list = man.m_objectsBySector[i];
                    if (list == null) continue;
                    for (int j = 0; j < list.Count; j++) if (list[j].Persistent) zdos.Add(list[j]);
                }
                foreach (var list in man.m_objectsByOutsideSector.Values)
                    for (int j = 0; j < list.Count; j++) if (list[j].Persistent) zdos.Add(list[j]);

                ZDOExtraData.PrepareSave();          // our prefix: alias, no clone
                ZoneSystem.instance.PrepareSave();
                RandEventSystem.instance.PrepareSave();

                ms = new MemoryStream(zdos.Count * 64);
                bw = new BinaryWriter(ms);
                bw.Write(man.m_sessionID);
                bw.Write(man.m_nextUid);
                bw.Write(zdos.Count);              // placeholder, patched below once skips are known
                pkg = new ZPackage();
                pkg.SetWriter(bw);
                setupOk = true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[ValheimTune] sliced save setup failed: " + e);
            }
            if (!setupOk)
            {
                ZDOExtraData.ClearSave();
                s_inProgress = false;
                yield break;
            }

            var sw = Stopwatch.StartNew();
            int slices = 0;
            bool itemFailed = false;
            Exception itemError = null;
            int saved = 0;

            // ponytail: sliced snapshot is consistent per ZDO but not across ZDOs within one save;
            // copy-on-write on the revision setters if a dupe/loss across two chests ever shows up
            foreach (bool _ in SaveSlicer.Run(zdos, z =>
                {
                    // Between slices the game runs: a ZDO in our list can be destroyed (Reset -> uid None)
                    // or its pooled slot reused for a new object. Vanilla's atomic clone never saw either.
                    // Skip anything that is no longer a live persistent object at the moment we reach it.
                    if (z.m_uid.IsNone() || !z.Persistent) return;
                    try { z.Save(pkg); saved++; }
                    catch (Exception e) { itemFailed = true; itemError = e; }
                },
                () => sw.Elapsed.TotalMilliseconds, Cfg.SaveSliceMs.Value))
            {
                slices++;
                if (itemFailed) break;
                if (!sync) yield return null;
            }

            if (itemFailed)
            {
                Plugin.Log.LogError("[ValheimTune] sliced save failed while serializing a ZDO: " + itemError);
                ZDOExtraData.ClearSave();
                s_inProgress = false;
                yield break;
            }

            try
            {
                bw.Flush();
                long end = ms.Position;
                ms.Position = sizeof(long) + sizeof(uint);   // count field, right after sessionID and nextUid
                bw.Write(saved);
                bw.Flush();
                ms.Position = end;
                s_buffer = ms.ToArray();
                s_count = saved;
                ZLog.Log($"[ValheimTune] sliced snapshot: {saved} ZDOs ({zdos.Count - saved} skipped as destroyed mid-save), {s_buffer.Length / 1024} KB, {(sync ? $"{slices + 1} slices back-to-back (sync)" : $"{slices} frames")}, {sw.Elapsed.TotalMilliseconds:F0} ms total");

                net.m_saveThreadStartTime = Time.realtimeSinceStartup;
                net.m_saveThread = new Thread(net.SaveWorldThread);
                net.m_saveThread.Start();
                if (sync) { net.m_saveThread.Join(); net.m_saveThread = null; net.m_sendSaveMessage = 0.5f; }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[ValheimTune] sliced save finalize/thread-start failed: " + e);
                ZDOExtraData.ClearSave();
            }
            finally
            {
                s_inProgress = false;
            }
        }
    }
}
