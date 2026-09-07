# ValheimTune

Makes a Valheim dedicated server with a big base and a handful of players feel
like a small one.

**Server-side only — players install nothing.** Every change is on the server,
the wire format is untouched, and vanilla clients connect exactly as before.

```
game     0.221.12 (network version 36), dedicated server only
needs    BepInEx 5.4.x
status   live on the reference server since 2026-09-07:
         690,000 objects, a 12,000-instance base, 2-6 players
```

## Why

Measured on that server, not modelled:

| | Vanilla | ValheimTune |
|---|---|---|
| Sync scan, per player per round | 4.1 ms | **0.07 ms** |
| Autosave | 381 ms freeze in one frame | **6 ms slices over ~350 frames** |
| Join sync cost | ~11 ms per call | **~5.5 ms** |
| Join stream rate | 1,261 objects/s | **3,617 objects/s** |
| Server frame rate | 30 fps, hard-capped | **60 fps** |
| Relayed updates, 2 players at a base | baseline | **~55 % fewer** |

Every knob defaults to vanilla except the ones proven live. What each number
comes from is in [How it works](#how-it-works).

## Requirements

- Valheim **dedicated server** (Steam app 896660). Not the in-client host.
- BepInEx 5.4.x for Valheim ([BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)).
- Game version listed in `[Compat] KnownGoodBuilds` (currently `0.221.12`).
  On any other version the plugin runs in vanilla + measurement mode and says
  so in the log.

## Install

1. Install BepInEx on the server. With the `lloesche/valheim-server` Docker
   image that is `BEPINEX=true` in `server.env`.
2. Drop `ValheimTune.dll` into `BepInEx/plugins/` (Docker:
   `config/bepinex/plugins/`).
3. Restart. The config file appears at `BepInEx/config/akoozie.valheimtune.cfg`.
4. Check the log for:

```
[ValheimTune] 0.5.0 loaded on game 0.221.12 (net 36), 14 methods patched, replacements on
[ValheimTune] SendZDOs window 10240/2048, 3 constants replaced (expected 3)
```

### Recommended settings

What runs on the reference server. Apply one at a time and read the stats line
between changes. Most knobs take effect within 5 seconds without a restart.

```ini
[Server]
TargetFrameRate = 60

[Sync]
SendWindowBytes = 32768
MinHeadroomBytes = 4096
AllPeersPerRound = true
RelayMinIntervalMs = 200

[Steam]
SendRateMaxBytesPerSec = 1048576
```

`DirtySets`, `SlicedSave` and `TopKSort` are already on by default.

### If the log says `replacements OFF`

```
[ValheimTune] game 0.222.x not in KnownGoodBuilds (0.221.12): replacement
patches inactive, running vanilla + measurement
```

Your server updated to a game build this plugin has not been verified against.
Nothing is broken — the version gate did its job and refused to run old patch
logic against new code. But the plugin is now only printing the stats line; none
of the optimizations are running. **The log line tells you your exact game
version**, which is what the gate compares against.

You have three options.

**1. Wait for a release that lists your version.** The safe one. Check the
[releases page](https://github.com/Akoozie/ValheimTune/releases); each one names
the game build it was verified against. Meanwhile the stats line still works, so
you keep the diagnostics.

**2. Force it on and accept the risk.** Add your version to the list:

```ini
[Compat]
KnownGoodBuilds = 0.221.12, 0.222.3
```

Restart. Harmony will refuse to patch any method whose signature changed and log
it, and the constant-swap transpiler self-aborts unless it matches exactly the
three constants it expects — so a *shape* change fails loudly rather than
silently. What it cannot catch is a method whose shape is unchanged but whose
*semantics* moved.

**Back up your world first, and turn the save patch off before you do this:**

```ini
[Save]
SlicedSave = false
```

`SlicedSave` replaces the world-save path. Every other patch affects
performance; that one affects your world file, and it is the only one where
being wrong costs you something you cannot restart your way out of.

**3. Build it yourself against the new server assemblies.** See
[Building from source](#building-from-source). If it works, please open an issue
saying which game build — that is what gets it into the next release.

Reports welcome either way. `DisableOnUnknownBuild = false` is the blunt version
of option 2; it forces every replacement patch on for *any* version, and carries
the same caveat with none of the record of what you tested.

## Reading the stats line

Every `LogIntervalSeconds`, prefixed `[ValheimTune]`:

```
frame avg 16.7 max 17.0 ms (60 fps) | syncList avg 0.07 max 0.20 ms | send avg 0.10 ms
| Z max 11418 | peer-sends 400 | zdos/s sent 430 recv 1000 | peers 2
| marks 15750 full 0 dirtyRounds 399 deferred 8300 drained 12 | meshSkips 0
recv by prefab (7848 in window): Fish1=1833 Fish2=1315 ...
hot objects: Wood=69@(-327,-631) ...
```

<details>
<summary><b>What each field means</b></summary>

| Field | Meaning | Healthy |
|---|---|---|
| frame | main-thread frame time | avg at your target, max under ~35 ms except during a join |
| syncList | time per candidate search, per player per round | ~0.1 ms steady, a few ms during a join |
| Z max | largest candidate set seen in a full scan | scales with your base |
| peer-sends | send calls in the window (rounds x players) | ~200 per player per 10 s with `AllPeersPerRound` |
| zdos/s sent / recv | last-second counters | recv is what your players' clients push; sent is the relay |
| marks | change-hook hits in the window | non-zero with players on; 0 means the hook is dead and the watchdog will fall back |
| full / dirtyRounds | full scans vs dirty-set rounds | full ~0, one per `ReconcileSeconds` per player |
| deferred | relays held back by the throttle | large is good |
| drained | candidates from the last dirty round | |
| meshSkips | render-mesh rebuilds skipped by `SkipRenderMesh` | climbs while players explore new ground, 0 elsewhere |
| DISABLED | appended if the watchdog tripped | should never appear |
| recv by prefab | which prefabs your players are pushing | tells you what to clean up |
| hot objects | per-object counts with world x,z | find the log that never stops rolling |

</details>

During a save you will also see:

```
[ValheimTune] sliced snapshot: 687131 ZDOs (0 skipped as destroyed mid-save), 30320 KB, 352 frames, 5956 ms total
```

<details>
<summary><b>Full config reference</b> — every knob, default, and when it takes effect</summary>

*runtime* = re-read every `ConfigReloadSeconds` without a restart.
*patch-time* = read once when the plugin loads; restart to change.

| Key | Default | When | What |
|---|---|---|---|
| `[Measure] LogIntervalSeconds` | 10 | runtime | Stats line cadence. 0 disables. |
| `[Measure] ConfigReloadSeconds` | 5 | runtime | How often the cfg is re-read. |
| `[Measure] HotObjectsIgnore` | Player,Fish1,Fish2,Fish3 | runtime | Prefabs left out of the hot-objects line. |
| `[Server] SkipRenderMesh` | false | runtime | Skip the heightmap render-mesh rebuild on a dedicated server; it is built for every zone a player explores and never drawn. Collision mesh untouched. |
| `[Server] TargetFrameRate` | 0 | runtime | Override the server's hard-coded 30 fps. 0 = leave it. Sync round period is `0.05 s + players / fps`. |
| `[Sync] SendWindowBytes` | 10240 | patch-time | Bytes in flight per player before the server stops queueing. Vanilla 10240. |
| `[Sync] MinHeadroomBytes` | 2048 | patch-time | Below this much free window the player is skipped this round. Keep below the window. |
| `[Sync] AllPeersPerRound` | false | runtime | Serve every player each round instead of one per frame. Pays off at 4+ players. |
| `[Sync] RoundSeconds` | 0.05 | runtime | Round period when `AllPeersPerRound` is on. |
| `[Sync] DirtySets` | **true** | runtime | Only consider changed objects each round. A watchdog falls back to vanilla if the change hook ever goes silent. |
| `[Sync] ReconcileSeconds` | 30 | per player at connect | Safety-net full scan interval. |
| `[Sync] RelayMinIntervalMs` | 0 | runtime | Re-send a non-prioritised object to the same player at most this often. 0 = vanilla. 200 is the tested value. |
| `[Sync] TopKSort` | **true** | runtime | Bounded-heap selection instead of a full sort of every candidate. |
| `[Sync] TopK` | 0 | runtime | Candidates ordered per round. 0 = `SendWindowBytes / 64`, never below 64. |
| `[Steam] SendRateMaxBytesPerSec` | 153600 | patch-time | Steam per-connection send cap. Vanilla 150 KB/s. Do not exceed your upload divided by player count. |
| `[Steam] SendRateMinBytesPerSec` | 153600 | patch-time | Leave at vanilla so Steam's estimator can back off on a lossy link. |
| `[Receive] MaxPacketsPerPeerPerFrame` | 0 | runtime | Stop draining one player's socket after this many packets in a frame. 0 = vanilla. Try 64 if one player's burst ever stalls the rest. |
| `[Save] SlicedSave` | **true** | runtime | Serialise the world on the main thread in slices; the writer thread never reads live game memory. |
| `[Save] SaveSliceMs` | 6 | runtime | Main-thread milliseconds per frame spent serialising during a save. |
| `[Cleanup] FloatingDropsRun` | false | one-shot | Set true to scan for item drops and felled logs floating in water. Resets itself. Dry run unless the next key is true. ~50 ms main-thread stall on a 698k-ZDO world. |
| `[Cleanup] FloatingDropsDelete` | false | runtime | With `Run`: delete what the scan finds. Hourly backups first. |
| `[Compat] KnownGoodBuilds` | 0.221.12 | patch-time | Game versions this plugin build was verified against. Comma-separated. |
| `[Compat] DisableOnUnknownBuild` | true | patch-time | On an unlisted version, run only measurement, the send-rate cap and the constant swap. |

</details>

## How it works

Each row is one measured problem and the patch that answers it.

<details>
<summary><b>Problem by problem</b></summary>

| Problem in vanilla | What ValheimTune does | Measured on a 690k-object world |
|---|---|---|
| Server is hard-capped at 30 fps, so every sync round waits for a slow frame | `TargetFrameRate` knob | 30 -> 60 fps, ~3 % CPU |
| Every sync round rescans every object near every player | **Dirty sets**: only objects that changed since the last round are considered; full scans only on join, zone change and every 30 s | 4.1 ms -> 0.07 ms per player per round |
| 10 KB send window and a hidden 150 KB/s per-connection Steam cap | Both raised, all players served every 50 ms instead of one per frame | Join streams 2.8x faster |
| Every update from every player is relayed to every other nearby player, ~17 times a second for anything that moves | **Relay throttle**: non-prioritised objects (fish, drifting items, pieces) re-sent to a given player at most every 200 ms; players and creatures exempt | ~55 % fewer relays with two players at the base |
| Autosave clones the whole world on the main thread, then a writer thread reads memory the game keeps changing (a torn-save race) | **Sliced save**: the world is serialised on the main thread in 6 ms slices into a buffer; the writer thread only writes | 381 ms freeze in one frame -> 6 ms slices over ~350 frames |
| During a join, every candidate object is fully sorted each round to pick the ~300 that fit | **Top-K selection**: a bounded heap keeps the best 512, no allocation | Join sync cost ~11 -> ~5.5 ms per call; closes a vanilla field-table leak on the way |
| A game update silently runs old patch logic on new code | **Version gate**: replacement patches only run on a build listed in the config; anything else logs a warning and runs vanilla plus measurement | Tested both ways on the live server |
| Hundreds of item drops and felled logs floating in water forever, each one a sync every round | One-shot scan and optional delete | 1,446 objects removed; idle inbound traffic 800 -> ~650 updates/s |

</details>

<details>
<summary><b>The 14 patched methods</b></summary>

Harmony patches on 14 methods of the dedicated-server assembly, all in
`Patches/`:

| Method | Patch | Purpose |
|---|---|---|
| `ZDOMan.CreateSyncList` | prefix + postfix | Dirty sets, relay throttle; timing |
| `ZDO.DataRevision` / `OwnerRevision` setters | postfix | Mark changed objects |
| `ZDOMan.ServerSortSendZDOS` | prefix | Top-K selection |
| `ZDOMan.SendZDOs` | transpiler + prefix/postfix | Window constants; timing |
| `ZDOMan.SendZDOToPeers2` | prefix | All players per round |
| `ZRpc.Update` | prefix | Receive cap |
| `ZSteamSocket.RegisterGlobalCallbacks` | postfix | Steam send rate |
| `ZDO.Deserialize` | postfix | Per-prefab tally |
| `Heightmap.RebuildRenderMesh` | prefix | Skip the render mesh on a headless server |
| `ZNet.SaveWorld`, `ZDOMan.PrepareSave`, `ZDOMan.SaveAsync`, `ZDOExtraData.PrepareSave` | prefix | Sliced save |

</details>

Every patch that *replaces* game logic checks the version gate first and runs
vanilla when it is off. Measurement, the send-rate cap and the constant swap
run regardless. The constant swap refuses to apply unless it matches exactly
the three constants it expects.

Pure logic (dirty-set state, the watchdog rule, the slicer, the top-K heap,
the version check) lives outside `Patches/` and has unit tests that run
without the game.

### Deliberate limits

- The sliced save is consistent per object, not across objects within one
  save; two objects can be up to a few seconds apart. Vanilla's clone was
  atomic across objects but tore individual ones. Objects destroyed mid-save
  are skipped and the count is patched.
- The relay throttle can delay a *repeated* update of a fish or a rolling log
  by up to 200 ms. First updates ship on the next round.
- Nothing here changes what a client is asked to render or simulate. Render
  distance, client-side rate limits and reliable/unreliable lanes would need
  a client mod and are out of scope.

## Building from source

Needs the .NET 8 SDK and the dedicated server's managed assemblies.

```
# point the build at your server's Managed folder (or copy the DLLs somewhere)
export GameManaged=/path/to/valheim_server_Data/Managed
dotnet build ValheimTune/ValheimTune.csproj -c Release
dotnet test ValheimTune.sln
```

Output: `ValheimTune/bin/Release/netstandard2.1/ValheimTune.dll`. Private game
members are publicised at compile time by `BepInEx.AssemblyPublicizer.MSBuild`;
BepInEx.Core and HarmonyX come from the BepInEx NuGet feed (`nuget.config`).

`deploy.sh` is the reference server's build-copy-restart loop; set `HOST` and
adapt the `docker` lines to your setup.

## When the game updates

See [`docs/PORTING.md`](docs/PORTING.md). Short version: the plugin notices,
switches its replacement patches off, and logs it. Players play vanilla plus
the stats line until a rebuild adds the new version to `KnownGoodBuilds`.

## License

MIT. Not affiliated with Iron Gate or Coffee Stain.

Reports from other servers welcome — open an issue with your stats line.
