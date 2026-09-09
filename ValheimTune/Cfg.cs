using BepInEx.Configuration;

namespace ValheimTune
{
    public static class Cfg
    {
        public static ConfigEntry<int> LogIntervalSeconds;
        public static ConfigEntry<int> SendWindowBytes, MinHeadroomBytes;
        public static ConfigEntry<bool> AllPeersPerRound;
        public static ConfigEntry<float> RoundSeconds;
        public static ConfigEntry<int> SendRateMax, SendRateMin;
        public static ConfigEntry<int> MaxPacketsPerPeerPerFrame;
        public static ConfigEntry<int> TargetFrameRate;
        public static ConfigEntry<int> ConfigReloadSeconds;
        public static ConfigEntry<bool> FloatingDropsRun, FloatingDropsDelete;
        public static ConfigEntry<string> HotObjectsIgnore;
        public static ConfigEntry<bool> DirtySets;
        public static ConfigEntry<float> ReconcileSeconds;
        public static ConfigEntry<int> RelayMinIntervalMs;
        public static ConfigEntry<string> KnownGoodBuilds;
        public static ConfigEntry<bool> DisableOnUnknownBuild;
        public static ConfigEntry<bool> TopKSort;
        public static ConfigEntry<int> TopK;
        public static ConfigEntry<bool> ServerSkipRenderMesh;
        public static ConfigEntry<bool> DeferAssetUnload;
        public static ConfigEntry<int> AssetUnloadMaxDeferMinutes;

        public static void Bind(ConfigFile c)
        {
            LogIntervalSeconds = c.Bind("Measure", "LogIntervalSeconds", 10, "How often to print the stats line. 0 disables.");
            ConfigReloadSeconds = c.Bind("Measure", "ConfigReloadSeconds", 5, "Re-read the cfg file this often so knobs that are read at runtime change without a restart. 0 disables. Patch-time settings (SendZDOs window, Steam send rate) still need a restart.");
            SendWindowBytes  = c.Bind("Sync", "SendWindowBytes", 10240, new ConfigDescription("Per-peer bytes in flight before the server stops queueing ZDO data. Vanilla 10240. Try 32768. Also sizes each iteration of the disconnect flush.", new AcceptableValueRange<int>(4096, 1048576)));
            MinHeadroomBytes = c.Bind("Sync", "MinHeadroomBytes", 2048, new ConfigDescription("Below this much free window the peer is skipped this round. Vanilla 2048. Scale with the window; must stay below SendWindowBytes.", new AcceptableValueRange<int>(256, 262144)));
            AllPeersPerRound = c.Bind("Sync", "AllPeersPerRound", false, "Serve every peer each round instead of one peer per frame. Turn on after B1 is verified with several players; otherwise it concentrates the scan cost into one frame (6 peers x 4.1 ms full scans per 0.05 s if the DirtySets watchdog ever disables dirty sets).");
            RoundSeconds     = c.Bind("Sync", "RoundSeconds", 0.05f, new ConfigDescription("Round period when AllPeersPerRound is on. Vanilla 0.05.", new AcceptableValueRange<float>(0.01f, 1f)));
            SendRateMax      = c.Bind("Steam", "SendRateMaxBytesPerSec", 153600, "Steam per-connection send rate cap. Vanilla 153600 (150 KB/s). Try 1048576. Bounded by your upload / player count.");
            SendRateMin      = c.Bind("Steam", "SendRateMinBytesPerSec", 153600, "Leave at vanilla so the estimator can back off on loss.");
            MaxPacketsPerPeerPerFrame = c.Bind("Receive", "MaxPacketsPerPeerPerFrame", 0, "Stop draining one peer's socket after this many packets in a frame; the rest wait in Steam's queue. 0 = vanilla (unlimited). Try 64.");
            TargetFrameRate = c.Bind("Server", "TargetFrameRate", 0, "Application.targetFrameRate for the headless server. 0 = do not touch. Round period is 0.05 + players/fps, so 30 -> 60 doubles the sync rate at 6 players. Costs CPU.");
            FloatingDropsRun = c.Bind("Cleanup", "FloatingDropsRun", false, "One-shot trigger: set true to scan for item drops floating in water. The plugin runs it on the next config reload and sets this back to false. Measured ~50 ms of main-thread stall on a 698k-ZDO world (one 67 ms frame, 2026-09-07).");
            FloatingDropsDelete = c.Bind("Cleanup", "FloatingDropsDelete", false, "When a scan runs with this true, the found items are DELETED (server takes ownership and destroys them; clients see them vanish). Leave false for a dry run that only logs counts.");
            HotObjectsIgnore = c.Bind("Measure", "HotObjectsIgnore", "Player,Fish1,Fish2,Fish3", "Comma-separated prefab names left out of the hot-objects line (things that are expected to move).");
            DirtySets        = c.Bind("Sync", "DirtySets", true, "B1: only consider changed ZDOs each round instead of rescanning the whole active area. Full scan on join, zone change, and every ReconcileSeconds.");
            ReconcileSeconds = c.Bind("Sync", "ReconcileSeconds", 30f, "Safety-net full scan interval per peer when DirtySets is on.");
            RelayMinIntervalMs = c.Bind("Sync", "RelayMinIntervalMs", 0, "Do not re-send a non-prioritized object (fish, items, pieces) to the same peer more often than this, in ms. 0 = vanilla. 200 = 5 Hz; fish and drifting items are the bulk of idle traffic at a big base.");
            KnownGoodBuilds = c.Bind("Compat", "KnownGoodBuilds", "1.0.7", "Game versions (Version.CurrentVersion) this build of the plugin was verified against. Comma-separated.");
            DisableOnUnknownBuild = c.Bind("Compat", "DisableOnUnknownBuild", true, "On a version not in KnownGoodBuilds, keep only measurement, the send-rate postfix and the constant swap; every replacement patch runs vanilla.");
            TopKSort = c.Bind("Sync", "TopKSort", true, "B2: bounded-heap selection of the objects that fit the send window instead of a full sort of every candidate. Only matters during joins, zone changes and the reconcile scan.");
            ServerSkipRenderMesh = c.Bind("Server", "SkipRenderMesh", false, "B4a: skip Heightmap.RebuildRenderMesh on a dedicated server. The render mesh is rebuilt for every ghost zone a player explores and every terrain edit that loads with one, and never drawn on a -nographics process. Collision mesh untouched. Watch meshSkips on the stats line.");
            DeferAssetUnload = c.Bind("Server", "DeferAssetUnload", false, "G1: hold the hourly Resources.UnloadUnusedAssets() until no players are connected. Vanilla runs it every hour regardless (Game.cs:239); measured 443-607 ms of main-thread stall on a 698k-ZDO world, landing with players online. Deferred, not skipped: it still runs the moment the server empties, or after AssetUnloadMaxDeferMinutes whichever comes first.");
            AssetUnloadMaxDeferMinutes = c.Bind("Server", "AssetUnloadMaxDeferMinutes", 240, new ConfigDescription("Backstop for DeferAssetUnload: run the collection even with players online once it has been held this long. Stops a server that never empties from never collecting.", new AcceptableValueRange<int>(10, 1440)));
            TopK     = c.Bind("Sync", "TopK", 0, new ConfigDescription("How many candidates to order per round. 0 = SendWindowBytes / 64 (512 at 32 KB), never below 64.", new AcceptableValueRange<int>(0, 8192)));
        }
    }
}
