Compatibility rebuild for **Valheim 1.0.20** (dedicated-server build `25909431`). No behaviour change; the code is 0.7.9's.

### Why you want it

On 1.0.20, 0.7.9 does not list the build in `KnownGoodBuilds`. With the default `DisableOnUnknownBuild = true` every replacement patch and every `[Fixes]` fix falls back to vanilla and only the stats line keeps running. 0.7.10 adds 1.0.20 to the shipped list.

### Network version is now 41

Players have to update their game to 1.0.20 to join a 1.0.20 server. That is the game's rule, not the plugin's; players still install nothing for ValheimTune.

### Upgrading

Drop the new DLL in and restart — **no config edit needed**. The shipped `KnownGoodBuilds` list is a floor, so your existing config keeps its patches.

### What this rests on

All 18 patched methods are identical between 1.0.17 and 1.0.20 (decompile diff). Outside them, the patched classes change in three places, none of which the plugin touches:

| File | What changed |
|---|---|
| `Version.cs` | version 1.0.20, network version 41, new save-format versions for the cheated-flag reset |
| `ZDOMan.cs` | `SaveChunk` writes chunk format 42; old-item conversion uses the new cheated-flag helpers (neither patched) |
| `ZoneSystem.cs` | one spawn line sets the cheated flag through a new helper (not patched) |
| `ZRpc.cs` `ZSteamSocket.cs` `ZDO.cs` `Game.cs` `Heightmap.cs` | **0** |

Builds against the 1.0.20 assemblies; 53/53 unit tests.

### Not verified live

Nothing has booted this DLL on a 1.0.20 server yet. Harmony attachment and the constant swap show in the first two boot log lines:

```
[ValheimTune] 0.7.10 loaded on game 1.0.20 (net 41), 18 methods patched, replacements on
[ValheimTune] SendZDOs window 10240/2048, 3 constants replaced (expected 3)
```
