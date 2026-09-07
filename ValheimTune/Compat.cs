using System;
using System.Linq;

namespace ValheimTune
{
    // Pure: no game types. Gates the replacement patches on a known game build.
    public static class Compat
    {
        public static bool ReplacementsAllowed = true;
        public static string GameVersion = "?";

        // Comma-separated list, whitespace tolerated, exact match on the trimmed entries.
        public static bool IsKnown(string version, string knownList)
        {
            if (string.IsNullOrEmpty(knownList)) return false;
            return knownList.Split(',').Any(s => s.Trim() == version);
        }
    }
}
