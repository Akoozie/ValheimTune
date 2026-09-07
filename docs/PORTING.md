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
  `DirtySets`, `AllPeersPerRound`, `SlicedSave`, `TopKSort`); a bad patch is
  one config line and a restart away from off. `ConfigReloadSeconds` makes runtime knobs live
  without a restart.
- Keep `tools/server-managed/` and `src_server/` from the build the plugin
  was last verified against, and tag the repo (`git tag v0.5.0-b21981590`)
  so the pair can be rebuilt.
