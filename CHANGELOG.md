# Changelog

All against dedicated-server build 21981590 (game 0.221.12, network version 36).

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
