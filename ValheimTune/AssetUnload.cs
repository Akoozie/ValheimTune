namespace ValheimTune
{
    // Pure decision logic for G1. No game types, so it unit-tests without the game assembly.
    //
    // Vanilla calls Resources.UnloadUnusedAssets() every hour (Game.cs:239 InvokeRepeating,
    // Game.cs:299 the 3599 s check). On a big world that is a 443-607 ms main-thread stall
    // measured on GalinBalin, and the cost is independent of what it frees: Unity's MarkObjects
    // walks all ~207,000 loaded objects to release one asset. It has been seen landing with two
    // players online (frame max 512 ms at 21:04:26, 2026-09-07).
    //
    // We do not skip it - memory hygiene still matters on an 8 GB host that already swaps this
    // world into zram. We move it to a moment nobody is playing, with a backstop so a server that
    // never empties still collects.
    public static class AssetUnload
    {
        public enum Decision { Run, Defer }

        // peers          ready peer connections right now
        // deferredFor    seconds since the first deferral of this collection, 0 if not deferred yet
        // maxDeferSecs   backstop: run anyway once a deferral has been held this long
        public static Decision Decide(int peers, double deferredFor, double maxDeferSecs)
        {
            if (peers <= 0) return Decision.Run;
            if (deferredFor >= maxDeferSecs) return Decision.Run;
            return Decision.Defer;
        }
    }
}
