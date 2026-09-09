> Written for the analysis repo this plugin came out of; `tools/refresh-server.sh`, `src_server/` and `mod/` refer to that layout. The logic applies unchanged: pull the new server build, diff the patched methods, add the version to `KnownGoodBuilds`, rebuild, test, deploy.

# Porting ValheimTune to a new game build (1.0 and every patch after)

The plugin is compiled against one exact dedicated-server build
(`tools/server-managed/`, build 21981590, game 0.221.12) and patches six
private game methods by name. A game update can break it three ways. Each has
a guard, and together they make an update a non-event: worst case, players get
vanilla plus the measurement line until the rebuild lands.

## How it breaks, and what catches it

| Break | Symptom | Guard |
|---|---|---|
| A patched member is renamed or removed | BepInEx logs a `TypeLoadException` / `MissingMethodException` at load; plugin does not load; server runs vanilla | Loud, safe by accident. The version gate (below) turns it into a clean "unknown build, inactive" line instead. |
| The constants inside `ZDOMan.SendZDOs` move | `[ValheimTune] SendZDOs window ..., N constants replaced (expected 3)` with N != 3 | Already logged. TODO: abort the swap when N != 3 instead of half-applying. |
| A method we *replaced* changes behaviour (`SendZDOToPeers2`, `ZRpc.Update`, `CreateSyncList`) | Nothing logs; the old logic silently runs on the new game | **The version gate.** Replacement patches enable only on a known-good build; measurement and the send-rate postfix stay on. |

## The version gate (to build, ~40 lines)

- Config: `[Compat] KnownGoodBuilds = "0.221.12"` (comma-separated game
  versions from `Version.GetVersionString()`), `[Compat] DisableOnUnknownBuild = true`.
- In `Plugin.Awake`, before `PatchAll`: read `Version.GetVersionString()`; if
  it is not in the list and the flag is on, log
  `[ValheimTune] game X not in KnownGoodBuilds: replacement patches inactive`
  and set a static `Compat.ReplacementsAllowed = false`. The three replacement
  prefixes check it first and return `true` (vanilla) when it is false. The
  transpiler aborts (returns the instructions unchanged) when
  `ConstSwap.LastReplaced != 3` and logs an error.
- The stats line prints the game version once at start so the log shows what
  ran against what.

## Release-day loop

```
1. Restore auto-update before release day:  UPDATE_CRON=*/15 * * * *  UPDATE_IF_IDLE=true
   (clients must update anyway when the network version bumps; the server cannot lag behind)
2. Server updates itself. Plugin sees an unknown build -> replacement patches off, logs it.
   Players play vanilla + measurement. Nothing breaks.
3. On the desktop:  tools/refresh-server.sh
   pulls the new build with SteamCMD, copies the DLLs to tools/server-managed/,
   decompiles into src_server/, diffs the six patched methods against the previous decompile.
4. Fix what moved (usually nothing or a constant), add the version to KnownGoodBuilds,
   bump the plugin version, dotnet test.
5. ./mod/deploy.sh  -> "[ValheimTune] x.y.z loaded, 8 methods patched",
   "3 constants replaced (expected 3)", stats line healthy, marks/dirtyRounds non-zero with a player on.
6. mod/DEPLOY-CHECKLIST.md step 2 numbers again; compare with the results table in OPTIMIZATION.md.
```

Time budget: 30 minutes if nothing moved, an evening if `SendZDOs` or
`CreateSyncList` were rewritten.

## What is version-sensitive, in order of likelihood to change

1. `ZDOMan.SendZDOs` constants (10240 x2, 2048): the transpiler map.
2. `ZDOMan.CreateSyncList` shape: near/distant gathering, the `< 10` rule.
3. `ZDOMan.SendZDOToPeers2`: the one-peer-per-frame scheduler.
4. `ZRpc.Update`: the receive loop and its two catch blocks.
5. `ZSteamSocket.RegisterGlobalCallbacks`: the send-rate config calls.
6. `ZDO.DataRevision` / `OwnerRevision` setters: if they stop being
   auto-properties the dirty-set hook needs a transpiler on `RPC_ZDOData`
   instead (design already in the B1 plan, Task 2 step 3).

Everything else the plugin touches is public API that has been stable for
years (`ZDO.GetPrefab/GetPosition/GetSector`, `ZNetScene.GetPrefab`,
`ZoneSystem.m_waterLevel`, `WorldGenerator.GetHeight`).

## Hotfix discipline

- One change per deploy; read the stats line for two minutes before the next.
- Every knob defaults to vanilla except the ones proven live (`TargetFrameRate`,
  `DirtySets`, `AllPeersPerRound`, `SlicedSave`, `TopKSort`); a bad patch is one config line and a
  restart away from off. `ConfigReloadSeconds` makes runtime knobs live
  without a restart.
- Keep `tools/server-managed/` and `src_server/` from the build the plugin
  was last verified against, and tag the repo (`git tag v0.5.0-b21981590`)
  so the pair can be rebuilt.

---

# What actually happened: 1.0.7, 2026-09-09

Valheim 1.0 shipped on 2026-09-09. The reference server took it the same day
(build `21981590` -> `25185644`, game `l-0.221.12` -> `l-1.0.7`, network
version 36 -> 39). BepInEx survived: the container's updater pulled
BepInExPack Valheim 5.4.2350 and the chainloader started clean on Unity
6000.0.75f1.

GalinBalin migrated without incident: 698,746 ZDOs loaded in 4,581 ms,
`ConvertInventories` converted 2,666 ZDOs and `ConvertContainers` 1,258.

The plugin did **not** run, and not because of the version gate. The BepInEx
reinstall rebuilt `/opt/valheim/bepinex` and symlinked only `config` back, so
the plugins directory came up empty and the chainloader logged
`0 plugins to load`. The DLL was then renamed to `ValheimTune.dll.disabled`
deliberately, to keep it off across restarts while the port is written.

> **Deploy trap.** `UPDATE_CRON` is empty and `UPDATE_IF_IDLE=false` on this
> host, so nothing auto-updates on a timer — but the updater still runs on
> *container bootstrap*. `./mod/deploy.sh` ends in `docker restart`, so
> deploying the mod and upgrading the game are the same button. Take a world
> backup before any deploy that follows a game release.

## Survey against the 1.0.7 dedicated-server assembly

Decompiled with the existing pipeline into `src_server_107/` (references in
`tools/server-managed-107/`, 135 assemblies vs 119 on 0.221.12).
`assembly_valheim.dll` grew 2,119,680 -> 2,557,952 bytes.

### The sync path is almost untouched

| File / member | 1.0.7 |
|---|---|
| `ZRpc.cs` | **byte-identical** to 0.221.12 |
| `ZSteamSocket.cs` | **byte-identical** |
| `ZDOMan.ServerSortSendZDOS` | identical body |
| `ZDOMan.SendZDOToPeers2` | identical body |
| `ZDOMan.SendZDOs` constants | same `10240 / 10240 / 2048`, moved line 714 -> 1060 |
| `ZDO.DataRevision` / `OwnerRevision` | still `{ get; set; }` auto-properties |
| `Game.CollectResources` | same signature, same 3600 s `InvokeRepeating` |
| `Heightmap.RebuildRenderMesh` | still present, still private |
| `ZDO.Deserialize(ZPackage)` | present |

`ZDO.DataRevision` staying an auto-property closes risk item 6 of the original
plan: the B1 dirty-set hook needs no transpiler fallback.

`CreateSyncList` changed in exactly two ways, and only one matters:

```diff
- Vector2i zone = ZoneSystem.GetZone(refPos);
+ Vector2s zone = ZoneSystem.GetZone(refPos);
- FindSectorObjects(zone, ZoneSystem.instance.m_activeArea, ZoneSystem.instance.m_activeDistantArea, ...)
+ FindSectorObjects(zone, peer.m_peer.m_simulationDistance, ...)
```

The full re-scan per round and the full sort of every candidate both survive,
so B1 and B2 still have the same target they were written against.

### (a) The save system was rewritten, and it supersedes S1

```
0.221.12                        1.0.7
PrepareSave()                   PrepareSave()
SaveAsync(BinaryWriter)    ->   SaveChunk(List<ZDO>, string, FileHelpers.FileSource)
                                SaveChunks(string, FileHelpers.FileSource)
                                SaveCleanup()
Load(BinaryReader, int)    ->   Load(BinaryReader, Version.World)
                                LoadChunks(string, FileHelpers.FileSource, Version.World)
```

`ZDOMan.SaveAsync` no longer exists — the one hard removal in the whole patch
surface. The world is now split into chunks (`ZoneSystem.ChunkIndex`), one
file each, with a dirty-chunk set (`m_dirtyChunks[0..1]`) and a save state
machine (`BeginSave` / `EndSave` / `UpdateSaveState`). Save format 37 -> 41.

The clone is **incremental**, which is the important part:

```
GetSaveClonePerChunk. Calculated number of actual chunk files: N
                      Number of dirty chunks to save: M [Xms]
```

S1 existed to stop the writer thread reading live ZDO arrays and to break the
main-thread clone into 6 ms slices. Iron Gate solved the same freeze from a
better angle — by not cloning or writing the clean 99% at all. **S1 should be
deleted, not ported.** Confirm against a live vanilla 1.0 autosave on
GalinBalin before deciding it is free.

### (b) Per-peer simulation distance (new, and the biggest lever in 1.0)

`SimulationDistance` is a new struct; it does not exist in 0.221.12. Clients
pick a level (default 2, from `PlatformPrefs "SimulationDistance"`), it is
synced, and it now drives `FindSectorObjects` — the loop that dominates sync
cost. `GetSimulationDistance(level)` maps 0 -> `(1,2,classic)`, 2 ->
`(2,2,classic)`, and falls through to `_ => new SimulationDistance(level, 2)`
with no upper clamp.

The server clamps it correctly on receipt, so this is not an abuse vector:

```csharp
// ZNet.cs:2126 RPC_RequestValidSimulationDistance
SimulationDistance sd = SimulationDistance.Deserialize(ref pkg);
SimulationDistance clamped = (sd < m_simulationDistance) ? sd : m_simulationDistance;
GetPeer(rpc).m_simulationDistance = clamped;
```

`min(client request, server cap)`. A peer can ask for less than the server
allows, never more.

### (c) Unverified lead: the server's own cap may never be initialised

On the dedicated-server build:

```csharp
ZNet.cs:220   private SimulationDistance m_simulationDistance;   // no initialiser
ZNet.cs:2096  private SimulationDistance GetDesiredSimulationDistance() { return m_simulationDistance; }
ZNet.cs:2097  if (desiredSimulationDistance.Equals(m_simulationDistance)) return;   // always true
```

`GetDesiredSimulationDistance()` returns the very field the handshake compares
against, so `SimulationDistanceServerHandshake()` early-returns on every call.
Nothing else on the server path assigns the field, and there is no cmdline
flag or config entry for it. Read literally, the server cap stays at
`default(SimulationDistance)` = near 0, far 0.

**This is a lead, not a finding.** It is a decompiled graphics-stubbed method,
and a regression this severe would be loudly visible on launch day. But it
makes a sharp prediction that costs one login to test: with a player online,
the sync radius would be roughly one zone and distant objects would barely
populate. Check `FindSectorObjects` behaviour with one peer before building
anything on top of it.

### (d) Minor

`Vector2i` -> `Vector2s` for zone indices: narrower zone keys, less memory
across ~700k ZDOs.

## Measured on 1.0.7, GalinBalin, 0 players

```
world load    loading ZDOs             4,581 ms
              converting/filtering     5,542 ms   (new in 1.0, migration pass)
              connecting ZDOs             37 ms
              total                   10,470 ms
SaveSystem.Reload (World)                 32 ms
idle          CPU 25.08%   RSS 1.894 GiB / 7.635 GiB
```

There is no 0.221.12 idle-memory baseline for this world to compare against;
the earlier ~1 GiB figure on record was measured on a far smaller world and is
not comparable.

First vanilla 1.0 autosave, 2026-09-09 15:55:49, 0 players:

```
GetSaveClonePerChunk   50 chunk files, 50 dirty      187 ms   main thread
PrepareSave (ZDOExtraData)                           211 ms   main thread
World save (1/5) cloud & backup checks                 2 ms
World save (2/5) chunks writing                    2,713 ms   writer thread
World save (3/5) DB2 writing                          50 ms
World save (4/5) FWL writing                           3 ms
idle after                    CPU 24.38%   RSS 2.025 GiB
```

Main-thread cost ~398 ms, against the ~600 ms `STATUS.md` estimated for
0.221.12 on this world. **All 50 chunks were dirty** — this was the first save
after a restart, so it is the full-save worst case and shows none of the
incremental benefit. A second autosave with few dirty chunks is what actually
measures the new design; until then, deleting S1 rests on the code argument,
not on this number.

The world is now a directory. `/config/worlds_local/GalinBalin/` holds the
chunk files; the monolithic `GalinBalin.db` survives only as `.db.old`. The
hourly backup job zips `worlds_local/` wholesale so it still captures
everything, but anything that assumed a single `.db` file needs revisiting.

### (e) `ZDOExtraData.PrepareSave` is now the whole main-thread save cost

Second vanilla autosave, 2026-09-09 16:25:49, 0 players, nothing changed since
the first:

```
                        save 1 (50/50 dirty)   save 2 (0/49 dirty)
GetSaveClonePerChunk           187 ms                 6 ms
ZDOExtraData.PrepareSave       211 ms               188 ms
chunks writing               2,713 ms                 3 ms
DB2 / FWL                       53 ms                19 ms
5-phase total                      -                 58 ms
chunk dir                          -      54 files, 21 MB (was 31.9 MB monolithic)
```

The incremental design works: writer cost collapsed 2,713 ms -> 3 ms and the
clone 187 ms -> 6 ms on a world where nothing changed. **This settles S1** -
deleting it was right, and vanilla's answer is far better than slicing.

But `ZDOExtraData.PrepareSave` did not move: ~188 ms on both saves, independent
of how many chunks are dirty. It is now ~97% of the main-thread save cost. The
method is **byte-identical between 0.221.12 and 1.0.7** - eight full `.Clone()`
calls over every extra-data map plus `RegenerateConnectionHashData()`. The
rewrite did not touch it.

The deleted S1 had a prefix for exactly this (alias, no clone). **It cannot be
restored on its own.** `ZNet.SaveWorld` still does:

```csharp
m_zdoMan.PrepareSave();                       // the clone
m_saveThread = new Thread(SaveWorldThread);   // reads the snapshot
m_saveThread.Start();
```

The clone is what makes the writer thread safe. Aliasing was only sound inside
S1 because S1 also replaced the write path so nothing read live arrays off the
main thread. Standalone, the alias reintroduces the torn-read race S1 existed
to avoid.

So this is a new, narrower problem, not a restore: cut the cost of cloning
eight maps that are mostly unchanged, without handing the writer live data.
Worth measuring with players online first - 188 ms every 30 minutes on an idle
server may not be worth any risk, and the figure may scale with churn rather
than world size.

## Port status

| Change | Why |
|---|---|
| `Version.m_networkVersion` -> `Version.c_networkVersion` | renamed, 36u -> 39u |
| delete `SavePatches.cs`, `SaveSlicer.cs`, `SaveSlicerTests.cs` | `ZDOMan.SaveAsync` gone; superseded by native chunked save |
| drop `[Save] SlicedSave`, `[Save] SaveSliceMs` | same |
| `[Compat] KnownGoodBuilds` default `0.221.12` -> `1.0.7` | gate reads `Version.CurrentVersion.ToString()` |
| build refs -> `tools/server-managed-107/` | 1.0.7 assemblies |

Everything else rebuilds unchanged.
