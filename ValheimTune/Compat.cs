using System;
using System.Linq;

namespace ValheimTune
{
    // Pure: no game types. Gates the replacement patches on a known game build.
    public static class Compat
    {
        public static bool ReplacementsAllowed = true;
        public static string GameVersion = "?";

        // Shipped default for [Compat] KnownGoodBuilds. Existing .cfg files keep their own value.
        public const string DefaultKnownGoodBuilds = "1.0.7, 1.0.12";

        // The shipped list is a floor: config can add builds, never remove one this
        // release was built for. BepInEx keeps an existing cfg on upgrade, so without
        // this every 0.7.0 install silently loses all patches the moment it hits 1.0.12.
        // Narrowing the list was never a documented way to disable anything -
        // DisableOnUnknownBuild and the per-feature knobs are.
        public static bool IsKnownOrShipped(string version, string knownList)
        {
            return IsKnown(version, knownList) || IsKnown(version, DefaultKnownGoodBuilds);
        }

        // Comma-separated list, whitespace tolerated, exact match on the trimmed entries.
        public static bool IsKnown(string version, string knownList)
        {
            if (string.IsNullOrEmpty(knownList)) return false;
            return knownList.Split(',').Any(s => s.Trim() == version);
        }
    }
}
