using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ValheimTune.Patches;

namespace ValheimTune
{
    [BepInPlugin(Guid, "ValheimTune", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "akoozie.valheimtune";
        public const string Version = "0.5.0";
        private const float WatchdogWindowSeconds = 10f;
        public static ManualLogSource Log;
        public static Plugin Instance;
        private Harmony _harmony;
        private float _logTimer;
        private float _reloadTimer;
        private float _wdTimer;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Cfg.Bind(Config);
            if (Cfg.MinHeadroomBytes.Value >= Cfg.SendWindowBytes.Value)
                Log.LogError("[ValheimTune] MinHeadroomBytes >= SendWindowBytes: no ZDO will ever be sent. Fix the config.");
            Compat.GameVersion = global::Version.CurrentVersion.ToString();      // "0.221.12"; GetVersionString may carry a platform prefix
            Compat.ReplacementsAllowed = !Cfg.DisableOnUnknownBuild.Value || Compat.IsKnown(Compat.GameVersion, Cfg.KnownGoodBuilds.Value);
            if (!Compat.ReplacementsAllowed)
                Log.LogWarning($"[ValheimTune] game {Compat.GameVersion} not in KnownGoodBuilds ({Cfg.KnownGoodBuilds.Value}): replacement patches inactive, running vanilla + measurement");
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            uint networkVersion = global::Version.m_networkVersion;
            Log.LogInfo($"[ValheimTune] {Version} loaded on game {Compat.GameVersion} (net {networkVersion}), {_harmony.GetPatchedMethods().Count()} methods patched, replacements {(Compat.ReplacementsAllowed ? "on" : "OFF")}");
        }

        private void Update()
        {
            ReloadConfig();
            RunFloatingDropsIfRequested();
            MeasurePatches.FrameMs.Add(UnityEngine.Time.unscaledDeltaTime * 1000f);
            int fps = Cfg.TargetFrameRate.Value;
            if (fps > 0 && UnityEngine.Application.targetFrameRate != fps)
            {
                UnityEngine.Application.targetFrameRate = fps;
                Log.LogInfo($"[ValheimTune] targetFrameRate set to {fps}");
            }
            var zdoMan = ZDOMan.instance;
            int recvNow = zdoMan == null ? 0 : zdoMan.GetRecvZDOs();
            _wdTimer += UnityEngine.Time.unscaledDeltaTime;
            if (_wdTimer >= WatchdogWindowSeconds)
            {
                long wdRecv = Patches.DirtyPatches.WatchdogRecv, wdMarks = Patches.DirtyPatches.WatchdogMarks;
                if (Patches.DirtyPatches.WatchdogShouldTrip(Cfg.DirtySets.Value, Patches.DirtyPatches.Disabled, wdRecv > 0, wdMarks))
                {
                    Patches.DirtyPatches.Disabled = true;
                    Log.LogError($"[ValheimTune] DirtySets: {wdRecv} ZDOs deserialized in {WatchdogWindowSeconds:F0} s but the revision hook fired 0 times; falling back to vanilla scanning. The ZDO revision setters are probably inlined on this Mono build.");
                }
                else if (Patches.DirtyPatches.WatchdogShouldRearm(Patches.DirtyPatches.Disabled, wdMarks))
                {
                    Patches.DirtyPatches.Disabled = false;
                    Log.LogWarning($"[ValheimTune] DirtySets: revision hook fired {wdMarks} times while disabled; re-armed (full scan per peer on the next round).");
                }
                Patches.DirtyPatches.WatchdogMarks = 0;
                Patches.DirtyPatches.WatchdogRecv = 0;
                _wdTimer = 0f;
            }
            int every = Cfg.LogIntervalSeconds.Value;
            if (every <= 0) { MeasurePatches.ResetAll(); DirtyPatches.ResetCounters(); Patches.RenderMeshPatch.Skipped = 0; return; }
            _logTimer += UnityEngine.Time.unscaledDeltaTime;
            if (_logTimer < every) return;
            _logTimer = 0f;
            int peers = zdoMan == null ? 0 : zdoMan.m_peers.Count;
            int sent = zdoMan == null ? 0 : zdoMan.GetSentZDOs();
            int recv = recvNow;
            Log.LogInfo(
                $"[ValheimTune] frame avg {MeasurePatches.FrameMs.Avg:F1} max {MeasurePatches.FrameMs.Max:F1} ms " +
                $"({1000f / System.Math.Max(0.01, MeasurePatches.FrameMs.Avg):F0} fps) | " +
                $"syncList avg {MeasurePatches.SyncListMs.Avg:F2} max {MeasurePatches.SyncListMs.Max:F2} ms | " +
                $"send avg {MeasurePatches.SendMs.Avg:F2} ms | Z max {MeasurePatches.MaxZ} | peer-sends {MeasurePatches.Rounds} | " +
                $"zdos/s sent {sent} recv {recv} | peers {peers}" +
                $" | marks {Patches.DirtyPatches.Marks} full {Patches.DirtyPatches.FullScans} dirtyRounds {Patches.DirtyPatches.DirtyRounds} deferred {Patches.DirtyPatches.Deferred} drained {Patches.DirtyPatches.LastDrained}{(Patches.DirtyPatches.Disabled ? " DISABLED" : "")}" +
                $" | meshSkips {Patches.RenderMeshPatch.Skipped}");
            if (MeasurePatches.Recv.Total > 0)
            {
                var sb = new System.Text.StringBuilder("[ValheimTune] recv by prefab (");
                sb.Append(MeasurePatches.Recv.Total).Append(" in window):");
                var scene = ZNetScene.instance;
                foreach (var kv in MeasurePatches.Recv.Top(8))
                {
                    string name = null;
                    if (scene != null) { var go = scene.GetPrefab(kv.Key); if (go != null) name = go.name; }
                    sb.Append(' ').Append(name ?? kv.Key.ToString()).Append('=').Append(kv.Value);
                }
                Log.LogInfo(sb.ToString());
                var ignoreSet = new System.Collections.Generic.HashSet<string>(
                    Cfg.HotObjectsIgnore.Value.Split(',').Select(s => s.Trim())
                );
                var hot = MeasurePatches.HotTop(12);
                var filtered = hot.Where(x => {
                    string name = (scene?.GetPrefab(x.Value.prefab)?.name ?? x.Value.prefab.ToString());
                    return !ignoreSet.Contains(name);
                }).Take(6).ToList();
                if (filtered.Count > 0)
                {
                    var hotSb = new System.Text.StringBuilder("[ValheimTune] hot objects:");
                    foreach (var kv in filtered)
                    {
                        string name = (scene?.GetPrefab(kv.Value.prefab)?.name ?? kv.Value.prefab.ToString());
                        int x = (int)kv.Value.pos.x;
                        int z = (int)kv.Value.pos.z;
                        hotSb.Append(' ').Append(name).Append('=').Append(kv.Value.count).Append('@').Append('(').Append(x).Append(',').Append(z).Append(')');
                    }
                    Log.LogInfo(hotSb.ToString());
                }
            }
            Patches.RenderMeshPatch.Skipped = 0;
            DirtyPatches.ResetCounters();
            MeasurePatches.ResetAll();
        }

        private void ReloadConfig()
        {
            int every = Cfg.ConfigReloadSeconds.Value;
            if (every <= 0) return;
            _reloadTimer += UnityEngine.Time.unscaledDeltaTime;
            if (_reloadTimer < every) return;
            _reloadTimer = 0f;
            try
            {
                Config.Reload();
            }
            catch (System.Exception e)
            {
                Log.LogError($"[ValheimTune] config reload failed: {e}");
            }
        }

        private void RunFloatingDropsIfRequested()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (ZDOMan.instance == null) return;
            if (!Cfg.FloatingDropsRun.Value) return;
            try
            {
                FloatingDrops.Run(Cfg.FloatingDropsDelete.Value, Log);
            }
            catch (System.Exception e)
            {
                Log.LogError($"[ValheimTune] floating drops run failed: {e}");
            }
            finally
            {
                Cfg.FloatingDropsRun.Value = false;
                Config.Save();
            }
        }
    }
}
