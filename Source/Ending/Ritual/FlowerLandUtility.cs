using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public static class FlowerLandUtility
    {
        private const float FieldDensity = 1f;
        private static readonly IntRange FieldColorCount = new IntRange(3, 4);
        private static readonly Color PreviewColor = new Color(1f, 0.85f, 0.45f);

        private static readonly List<IntVec3> _tmpCells = new List<IntVec3>();

        public static bool CanConvert(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map)) { return false; }

            var terrainGrid = map.terrainGrid;
            var terrain = terrainGrid.TerrainAt(cell);
            if (terrain.IsWater || terrainGrid.BaseTerrainAt(cell).IsWater) { return false; }
            if (terrain.passability == Traversability.Impassable) { return false; }
            if (terrainGrid.FoundationAt(cell) != null) { return false; }

            var edifice = cell.GetEdifice(map);
            return edifice == null || edifice.def.building == null || !edifice.def.building.isNaturalRock;
        }

        public static IEnumerable<IntVec3> ConvertibleCells(Map map, IntVec3 center, float radius)
        {
            return GenRadial.RadialCellsAround(center, radius, true).Where(c => CanConvert(map, c));
        }

        public static void DrawPreview(Map map, IntVec3 center, float radius)
        {
            if (map == null || !center.InBounds(map)) { return; }

            GenDraw.DrawRadiusRing(center, radius);

            _tmpCells.Clear();
            _tmpCells.AddRange(ConvertibleCells(map, center, radius));
            GenDraw.DrawFieldEdges(_tmpCells, PreviewColor);
            _tmpCells.Clear();
        }

        public static int Apply(Map map, IntVec3 center, float radius, TerrainDef terrain)
        {
            var converted = 0;
            foreach (var cell in ConvertibleCells(map, center, radius).ToList())
            {
                if (map.terrainGrid.TerrainAt(cell) != terrain)
                {
                    map.terrainGrid.SetTerrain(cell, terrain);
                }

                if (ModsConfig.BiotechActive && cell.IsPolluted(map))
                {
                    cell.Unpollute(map);
                }

                converted++;
            }

            if (converted == 0) { return 0; }

            ViviaFieldGenerator.Generate(map, center, radius, FieldDensity, FieldColorCount.RandomInRange, fullBloom: true, clipToRadius: true);
            return BloomViviaAround(map, center, radius);
        }

        private static int BloomViviaAround(Map map, IntVec3 center, float radius)
        {
            var count = 0;
            foreach (var cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map) || !(cell.GetPlant(map) is Plant plant) || !ArcanePlantUtility.ViviFlowerDefs.Contains(plant.def)) { continue; }

                if (plant.Growth < 1f)
                {
                    plant.Growth = 1f;
                    plant.DirtyMapMesh(map);
                }

                count++;
            }

            return count;
        }
    }
}
