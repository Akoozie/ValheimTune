# Changelog

0.7.0 and later target dedicated-server build 25185644 (game 1.0.7, network
version 39). 0.6.0 and earlier target build 21981590 (game 0.221.12, network
version 36).

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
