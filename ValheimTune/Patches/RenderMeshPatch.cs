using HarmonyLib;

namespace ValheimTune.Patches
{
    // B4a: a headless server runs Heightmap.Regenerate for every ghost zone a player explores
    // (ZoneSystem.SpawnZone instantiates the zone prefab, whose Heightmap regenerates in OnEnable
    // at src_server/Heightmap.cs:267) and for every terrain edit that loads with one
    // (TerrainComp.Poke). Regenerate rebuilds the collision mesh, which the server needs, and then
    // the render mesh, which it never draws: (m_width+1)^2 vertices, colours, UVs and indices plus
    // RecalculateNormals/Tangents/Bounds, on a -nographics process.
    //
    // m_renderMesh is private to Heightmap and every other read of it is null-guarded
    // (Clear at :1215, OnDestroy at :249), so leaving it null is safe. The collision mesh,
    // m_paintMask and the material instance are untouched.
    [HarmonyPatch(typeof(Heightmap), nameof(Heightmap.RebuildRenderMesh))]
    public static class RenderMeshPatch
    {
        public static long Skipped;

        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!Compat.ReplacementsAllowed || !Cfg.ServerSkipRenderMesh.Value) return true;
            if (ZNet.instance == null || !ZNet.instance.IsDedicated()) return true;
            Skipped++;
            return false;
        }
    }
}
