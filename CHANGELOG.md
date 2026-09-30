# Changelog

0.7.7 targets dedicated-server build 25527701 (game 1.0.16, network version
40), the same build as 0.7.5 and 0.7.6, and remains valid for 1.0.15, 1.0.14, 1.0.12 and
1.0.7. 0.7.4 targets
build 25390671 (game 1.0.15, network version 40). 0.7.3 targets build
25364309 (game 1.0.14, network version 40). 0.7.1 targets build 25253791 (game
1.0.12, network version 40). 0.7.0 targets build 25185644 (game 1.0.7, network
version 39). 0.6.0 and earlier target build 21981590 (game 0.221.12, network
version 36).

## 0.7.7 - 2026-09-30

Vanilla bug fixes and review fixes. No config edit needed; every new fix is on by
default and has its own switch under `[Fixes]`. Found by a full review of the
plugin and a first read of the server code it had never looked at; the complete
record, including what was checked and found clean, is in the analysis repo's
`docs/AUDIT-2026-09-30.md`.

**Vanilla bugs fixed (any 1.0 server has these):**

- **Player edits skipped by the incremental save** (`SaveDirtyFix`). Valheim 1.0
  saves only chunks marked dirty, and only changes the *server* makes mark them.
  Updates arriving from players - building, chests, signs - never do. An area
  only players touched since the last save can be skipped, and it reverts on
  restart while the character keeps the items. It usually hides behind
  incidental marks (ownership changes, objects crossing zones).
- **Spawners duplicating creatures after a restart** (`SpawnerLinkFix`). Every
  save gives spawner/creature links new hashes, but an unsaved chunk keeps the
  old ones; a link split across two chunks no longer matches at load and the
  spawner spawns another creature. Both sides are now saved together. The load
  log line `Removed connection from N orphan spawn:s` shows the vanilla bug; on
  our own vanilla world it read 0 at every restart for a month, so it is rare.
- **100 ms server freeze on every disconnect** (`DisconnectNoSleep`, restart to
  apply). `ZSteamSocket.Close` sleeps the main thread for 100 ms - for every
  player leaving, timing out or being refused at join. Removed; the connection
  closes with Steam's linger so queued messages still go out.
- **Destroyed-object records kept until restart** (`DeadZdoPrune`): pruned to
  the last hour at each save.
- **Repeat global-key broadcasts** (`GlobalKeyDedupe`): vanilla compares keys
  case-sensitively, so e.g. every unshielded ship in the Ashlands ocean
  re-broadcasts every global key to every player every 10 s.

**Fixed in the plugin:**

- Dirty sets could hold back distant objects (the far ring) until a player came
  near, whenever a full scan found 10 or more nearby objects to send - most of
  the time while travelling.
- Changing Simulation Distance in the graphics settings now triggers a full
  scan; newly in-range objects no longer wait up to 30 s.
- `SendWindowBytes` max is now 262144. Above ~450 KB a single package exceeds
  Steam's 512 KB limit and the player times out. Existing configs above the new
  max are clamped on load.
- `MinHeadroomBytes >= SendWindowBytes` now leaves `SendZDOs` vanilla instead of
  applying a window that sends nothing.
- Turning `DeferAssetUnload` off while a collection was held no longer causes an
  immediate unload stall when it is turned back on later.

**New on the stats line:** `release max` / `removePeer max` (ms), `gc` /
`heap`, `dead`, `saveMarks`, `linkFixes`, `keyDedupes`. The first two measure
two suspected stalls (the ownership pass every 2 s, and the disconnect cleanup
that walks every object) before anything is changed there.

The log now reads `18 methods patched`.

**Not verified live.** 53/53 unit tests; the disconnect transpiler was checked
against the real 1.0.16 IL of `ZSteamSocket.Close`. Nobody has booted 0.7.7.

## 0.7.6 - 2026-09-30

Bug fix. No config change, no new setting.

**Why you want it:** with dirty sets on (the default), an object the *server*
creates never reached players until the next safety-net full scan, up to
`ReconcileSeconds` (30 s) later. Server-side mods that recreate objects hit
this every time: ServersideQoL PrefabConfigurator destroys and recreates
building pieces to apply its changes, and with 0.7.5 each piece vanished for
15-20 s instead of a fraction of a second
([#3](https://github.com/Akoozie/ValheimTune/issues/3)).

**Cause:** dirty sets learn about changes from the `DataRevision` /
`OwnerRevision` setters. A ZDO built with `ZDOMan.CreateNewZDO` and filled with
`Deserialize` or `SetOwnerInternal` touches neither. Objects players place were
never affected - they arrive through `RPC_ZDOData`, which sets the revision.

**Fix:** a postfix on `ZDOMan.CreateNewZDO` marks every new object for every
player. All creation paths go through that one method. The log now reads
`12 methods patched`, and the stats line's `marks` count now includes object
creations, so it runs higher during building or zone generation. Workaround on
0.7.5: `[Sync] DirtySets = false`.

**Not verified live.** Unit tests pass (47/47); nobody has booted 0.7.6.

## 0.7.5 - 2026-09-25

Compatibility rebuild for Valheim 1.0.16 (dedicated-server build 25527701).
No behaviour change.

**Why you want it:** on 1.0.16, 0.7.4 does not list the build in
`KnownGoodBuilds`, so with the default `DisableOnUnknownBuild = true` every
replacement patch falls back to vanilla and only the stats line keeps running.
0.7.5 adds 1.0.16 to the shipped list. No config edit needed.

**Network version is still 40**, so 1.0.16 locks no one out.

**Not verified live.** Same caveat as 0.7.1 to 0.7.4. A decompile diff of 1.0.16
against 1.0.15 changes 19 files, all gameplay or client side: `SaveSystem.cs`
(cloud-only mount on backup restore), `SpawnSystem.cs` (spawn-hash counter fix),
`TerrainComp.cs` (duplicate handling again), `Player.cs`, `Achievements.cs`,
`ObjectDB.cs`, `PlayerProfile.cs` (food-achievement exclusions), a handful of
UI and gamepad files, and `Version.cs`. Every file this plugin patches -
`ZDOMan.cs`, `ZRpc.cs`, `ZSteamSocket.cs`, `ZDO.cs`, `Game.cs`, `Heightmap.cs`,
`ZoneSystem.cs` - is unchanged.

## 0.7.4 - 2026-09-19

Compatibility rebuild for Valheim 1.0.15 (dedicated-server build 25390671).
No behaviour change.

**Why you want it:** on 1.0.15, 0.7.3 does not list the build in
`KnownGoodBuilds`, so with the default `DisableOnUnknownBuild = true` every
replacement patch falls back to vanilla and only the stats line keeps running.
0.7.4 adds 1.0.15 to the shipped list. No config edit needed.

**Network version is still 40**, so 1.0.15 locks no one out.

**Not verified live.** Same caveat as 0.7.1 to 0.7.3. A decompile diff of 1.0.15
against 1.0.14 changes six files: `TerrainComp.cs` (the duplicated-terrain fix),
`Inventory.cs`, `InventoryGrid.cs`, `InventoryGui.cs`, `ItemDrop.cs` (the
cheated-item fix), and `Version.cs`. Every file this plugin patches - `ZDOMan.cs`,
`ZRpc.cs`, `ZSteamSocket.cs`, `ZDO.cs`, `Game.cs`, `Heightmap.cs`, `ZoneSystem.cs` -
is unchanged.

## 0.7.3 - 2026-09-17

Compatibility rebuild for Valheim 1.0.14 (dedicated-server build 25364309).
No behaviour change.

**Network version is still 40**, the same as 1.0.12, so unlike the 1.0.12
patch this one does not lock older servers out. A 1.0.12 server keeps accepting
1.0.14 clients; update when it suits you.

**Not verified live.** Same caveat as 0.7.1 and 0.7.2. What it rests on: a
decompile diff of the 1.0.14 dedicated-server assembly against 1.0.12 shows
every file this plugin patches is unchanged outright - `ZDOMan.cs`, `ZRpc.cs`,
`ZSteamSocket.cs`, `ZDO.cs`, `Game.cs`, `Heightmap.cs` and `ZoneSystem.cs` all
diff to zero lines. The anchors are at identical line numbers: `CreateSyncList`
(1261), `ServerSortSendZDOS` (1360), `SendZDOToPeers2` (886), the `SendZDOs`
window constants 10240/10240/2048 (1060/1064/1065), and `ZDO.DataRevision` /
`OwnerRevision` still auto-properties. 1.0.14 is a broad patch - 32 source files
changed, in combat, inventory, UI and settings - but none of it is in the sync
or networking path. The only networking-adjacent edit is `ZNet.Save`, which
1.0.14 makes save the player profile on a client-issued save; this plugin does
not patch it.

It compiles against the 1.0.14 assemblies and 47/47 unit tests pass. What stays
unproven is IL-level and a source diff cannot settle it: that Harmony attaches
to all 11 methods, and that the constant-swap transpiler still finds exactly 3
constants. Both show in the first two log lines on boot - if the load line does
not say `11 methods patched` or the window line does not say
`3 constants replaced (expected 3)`, set `[Compat] DisableOnUnknownBuild = true`
and report it.

- `[Compat] KnownGoodBuilds` shipped list is now `1.0.7, 1.0.12, 1.0.14`. It is
  a floor, so an existing config keeps its patches without an edit.

## 0.7.2 - 2026-09-13

Plays nicely with other networking mods ([#1](https://github.com/Akoozie/ValheimTune/issues/1)).

- **Vanilla now means hands off.** The Steam send-rate postfix used to write both
  rates on every boot, even at the vanilla 153600, which could reset a rate another
  networking mod had just set. Each rate is now only written when it differs from
  vanilla. Likewise the `SendZDOs` window transpiler leaves the method untouched
  while `SendWindowBytes` and `MinHeadroomBytes` are both vanilla. No change for
  anyone who tuned these values.
- **New `[Steam] OverrideSendRate` and `[Sync] OverrideSendWindow`** (default
  `true`). Set either to `false` to stop ValheimTune touching that setting at all,
  whatever the values say. Patch-time: restart to apply. The load log says which
  one left the game alone and why.
- Four new tests (47 total).

**Not verified live.** Same caveat as 0.7.1.

## 0.7.1 - 2026-09-11

Compatibility rebuild for Valheim 1.0.12 (build 25253791, network version 40).
No behaviour change.

**Not verified live.** 0.7.0 was booted on a real server with a player online;
0.7.1 has not been. What it does rest on: a decompile diff of the 1.0.12
dedicated-server assembly shows every method this plugin patches is
byte-identical to 1.0.7, at identical line numbers - `CreateSyncList` (1261),
`ServerSortSendZDOS` (1360), the `SendZDOs` window constants 10240/10240/2048
(1060/1064/1065), and `ZDO.DataRevision` / `OwnerRevision` still auto-properties.
`ZRpc.cs`, `ZSteamSocket.cs`, `ZDO.cs`, `Game.cs` and `Heightmap.cs` are
unchanged outright. The only ZDOMan edits in 1.0.12 are in
`ConvertInventories` / `ConvertContainers`, the world-migration path, which this
plugin does not touch. It compiles against the 1.0.12 assemblies and passes
40/40 unit tests. The gap that leaves: nobody has confirmed Harmony attaches at
runtime on 1.0.12, or that the constant-swap transpiler still finds exactly 3
constants. Read the load line on first boot and treat a
`constants replaced (expected 3)` mismatch as a reason to set
`DisableOnUnknownBuild = true` and report it.

- **The shipped version list is now a floor, not a default.** BepInEx keeps an
  existing config on upgrade, so before this change every 0.7.0 install would
  have kept `KnownGoodBuilds = 1.0.7` and silently dropped every patch the
  moment it reached 1.0.12 - an upgrade that quietly does nothing. The gate now
  passes if the build is in the user's list *or* in
  `Compat.DefaultKnownGoodBuilds` (`1.0.7, 1.0.12`), and logs once when the
  shipped list is what allowed it. Configs can still add builds; narrowing the
  list was never a documented way to disable anything - that is
  `DisableOnUnknownBuild` and the per-feature knobs.
- Builds against `tools/server-managed-1012/`; `src_server_1012/` added.
- Five new tests (43 total, was 38).

## 0.7.0 — 2026-09-09

Port to Valheim 1.0.7, deployed and verified live on 2026-09-09 with a player
online: `11 methods patched, replacements on`, `3 constants replaced
(expected 3)`, sync cost 0.09 ms/call against 4.1 ms vanilla, frame 16.7 avg /
17.0 max, no errors. B4a's `meshSkips` fired for the first time (34), 1.0 zone
generation finally giving it virgin terrain. **G1 verified at 18:54** - the
hour boundary landed with a player on and the unload was deferred, frames
staying 16.9-24.6 ms with none of the 443-607 ms stall seen on 0.221.12.
Every feature in the port is verified live. See `docs/PORTING-1.0.md` for the
full survey and the live numbers.

- **S1 sliced save removed.** 1.0 rewrote the save path: `ZDOMan.SaveAsync` is
  gone, replaced by `SaveChunks`/`SaveChunk`/`SaveCleanup` writing one file per
  chunk, and `GetSaveClonePerChunk` clones only *dirty* chunks. That is a
  strictly better answer to the freeze S1 was working around, so the feature is
  deleted rather than ported: `SavePatches.cs`, `SaveSlicer.cs`,
  `SaveSlicerTests.cs`, and the `[Save] SlicedSave` / `[Save] SaveSliceMs`
  knobs. Vanilla 1.0 autosave cost on a 698k-ZDO world is still unmeasured.
- **B1 dirty sets ported to per-peer simulation distance.** 1.0 removed
  `ZoneSystem.m_activeArea` / `m_activeDistantArea`; the sync radius is now a
  per-peer `SimulationDistance` negotiated in
  `ZNet.RPC_RequestValidSimulationDistance` and clamped server-side to
  `min(client request, server cap)`. `DirtyPatches` now mirrors the ring
  predicate in `ZDOMan.FindSectorObjects` (Chebyshev ring **and**
  `ZonesWithinRadius`, except in classic mode) instead of calling the removed
  `ZNetScene.InActiveArea(sector, zone, radius)`. Zone indices are `Vector2s`,
  not `Vector2i`. New `ZoneRingTests` cover the ring maths.
- `Version.m_networkVersion` renamed to `Version.c_networkVersion`.
- `[Compat] KnownGoodBuilds` default 0.221.12 -> 1.0.7.
- Unchanged and rebuilt as-is: B2 top-K, R2 relay throttle, the `SendZDOs`
  constant swap, the receive cap, the Steam send rate, `TargetFrameRate`,
  G1 deferred asset unload, B4a skip render mesh, the watchdog and the stats
  line. `ZRpc.cs` and `ZSteamSocket.cs` are byte-identical between 0.221.12 and
  1.0.7; `ServerSortSendZDOS` and `SendZDOToPeers2` have identical bodies.
- 38 unit tests.

## 0.6.0 — 2026-09-07
- G1 deferred asset unload (`[Server] DeferAssetUnload`, default off): vanilla runs `Resources.UnloadUnusedAssets()` every hour (`Game.cs:239` `InvokeRepeating`, the 3599 s check at `Game.cs:299`). Measured on GalinBalin at **443-607 ms of main-thread stall**, and the cost is independent of what it frees - Unity's `MarkObjects` walks all ~207,000 loaded objects to release one asset. Caught live at 21:04:26 with two players online (`frame max 512 ms`) and at 22:40:23 idle (`frame max 457 ms`, matching Unity's own 455.78 ms line). Both vanilla entry points route through `Game.CollectResources`, so one prefix covers them. The collection is **deferred, not skipped**: it runs the moment the last player disconnects, or after `AssetUnloadMaxDeferMinutes` (default 240) if the server never empties. Found by reading SmoothServer's module list; the deferral policy is ours.
- 33 unit tests.

## 0.5.0 — 2026-09-07
- B4a skip render mesh (`[Server] SkipRenderMesh`, default off): prefix on `Heightmap.RebuildRenderMesh` that returns false on a dedicated server. The server regenerates a heightmap for every ghost zone a player explores (`ZoneSystem.SpawnZone` -> the zone prefab's `Heightmap.OnEnable`) and for every terrain edit that loads with one (`TerrainComp.Poke`); the render half is `(m_width+1)^2` vertices, colours, UVs and indices plus `RecalculateNormals`/`Tangents`/`Bounds` on a `-nographics` process. The collision mesh, paint mask and material instance are untouched, and every other read of `m_renderMesh` is null-guarded. New `meshSkips` counter on the stats line. Off until a live exploration run shows the counter climbing with nothing visibly wrong.

## 0.4.2 — 2026-09-07
- Watchdog false trip fixed: "received" is now counted by our own `ZDO.Deserialize` postfix over the same window as the marks, instead of the game's one-second-lagging counter, which tripped it when the last player logged out (live at 21:00 with three players on, B1 and the throttle silently off). The watchdog also re-arms itself when marks reappear.

## 0.4.1 — 2026-09-07
- `TopKSort` default on after a live join: syncList ~11 -> ~5.5 ms avg, 24-32 -> 12-20 ms max. Gate tested both ways on the live server.

## 0.4.0 — 2026-09-07
- Version gate: replacement patches run only on a build in `[Compat] KnownGoodBuilds`; unknown build logs a warning and runs vanilla + measurement. Constant-swap transpiler aborts unless exactly 3 constants matched.
- B2 top-K selection (`TopKSort`, default off): bounded heap instead of a full sort of every candidate; closes the vanilla `m_tempSortValue` leak as a side effect. One `GetZDO` per id in the dirty-set drain.

## 0.3.1 — 2026-09-07
- `SlicedSave` default on after the live round-trip (687,133 saved at shutdown, 687,133 loaded). Sync-mode log line says "slices back-to-back" instead of "frames".

## 0.3.0 — 2026-09-07
- S1 sliced save (`SlicedSave`, default off): world serialized on the main thread in SaveSliceMs slices into a memory buffer; the writer thread no longer reads live ZDO arrays. Off until a live round-trip is verified.

## 0.2.2 — 2026-09-07
- Watchdog uses its own 10 s window and counter; no longer races the stats-line reset (would have disabled B1 after restarts).

## 0.2.1 — 2026-09-07
- Final-review fixes: all-peers round respects the watchdog; marks split by setter; forced full scan when DirtySets is re-enabled; ReconcileSeconds read per call; floating scan cutoff 0.25 m above water and logs positions; `drained` on the stats line; ZDOID.None ignored in Mark. `AllPeersPerRound` default back to false pending a multi-player test.

## 0.2.0 — 2026-09-07
- B1 dirty sets: per-peer pending set fed by the revision setters; full scan on join, zone change and every 30 s; watchdog falls back to vanilla if the hook is silent. Live: sync cost per call 4.1 ms -> 0.07 ms.
- R2 relay throttle (`RelayMinIntervalMs`), non-prioritized objects only.
- `DirtySets` default on.

## 0.1.5 — hot-objects ignore list.
## 0.1.4 — hot-objects line with world coordinates.
## 0.1.3 — floating scan includes felled logs (`TreeLog`).
## 0.1.2 — floating item-drop scan/delete; config reload every 5 s.
## 0.1.1 — per-prefab tally of received ZDOs.

## 0.1.0 — 2026-09-07
- Measurement stats line; `ConstSwap` IL constant swap; Tier C knobs (send window, headroom, all-peers round, Steam send rate); per-peer packet cap; `TargetFrameRate` knob. Final review fixes: restart grace 150 s, config ranges, per-peer try/catch, measurement hygiene.
