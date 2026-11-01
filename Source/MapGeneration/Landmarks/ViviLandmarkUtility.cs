using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public static class ViviLandmarkUtility
    {
        private const float BaselineMapArea = 250f * 250f;

        // 모드 바이옴도 지원하도록 고정 목록 대신 바이옴 속성과 기온으로 비비아 꽃의 생장 조건을 판정한다.
        public static bool IsViviaGrowableTile(PlanetTile tile, PlanetLayer layer, ViviLandmarkExtension extension)
        {
            if (extension == null) { return false; }

            var biome = layer[tile].PrimaryBiome;
            if (biome == null || biome.inVacuum || biome.isBackgroundBiome || biome.isExtremeBiome) { return false; }
            if (biome.plantDensity < extension.minPlantDensity) { return false; }

            var plant = VVThingDefOf.VV_Plant_FireArcaneFlower.plant;
            var growingTwelfths = GenTemperature.TwelfthsInAverageTemperatureRange(tile, plant.minOptimalGrowthTemperature, plant.maxOptimalGrowthTemperature);
            return growingTwelfths.Count >= extension.minGrowingTwelfths;
        }

        public static int CountForMap(Map map, ViviLandmarkExtension extension)
        {
            var scale = map.Size.x * map.Size.z / BaselineMapArea;
            return Mathf.Max(extension.minCount, GenMath.RoundRandom(extension.countRange.RandomInRange * scale));
        }

        public static List<IntVec3> FindSpacedCells(Map map, int count, float minSpacing, Predicate<IntVec3> validator)
        {
            var result = new List<IntVec3>();
            if (count <= 0) { return result; }

            foreach (var cell in map.AllCells.InRandomOrder())
            {
                if (result.Any(c => c.InHorDistOf(cell, minSpacing))) { continue; }
                if (!validator(cell)) { continue; }

                result.Add(cell);
                if (result.Count >= count) { break; }
            }

            return result;
        }

        public static bool IsInEdgeMargin(IntVec3 c, Map map, int margin)
        {
            return c.x < margin || c.z < margin || c.x >= map.Size.x - margin || c.z >= map.Size.z - margin;
        }

        public static bool IsAreaUnused(IntVec3 center, float radius)
        {
            var area = CellRect.CenteredOn(center, Mathf.CeilToInt(radius));
            return !MapGenerator.UsedRects.Any(rect => rect.Overlaps(area));
        }

        public static bool IsClearOfStructures(IntVec3 center, float radius, Map map)
        {
            foreach (var cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map)) { return false; }

                foreach (var thing in cell.GetThingList(map))
                {
                    if (thing.def.category == ThingCategory.Item || thing.def.category == ThingCategory.Filth) { return false; }
                    if (thing.def.category == ThingCategory.Building && thing.def.building?.isNaturalRock != true) { return false; }
                }
            }

            return true;
        }

        public static float ViviaPlantableRatioAround(IntVec3 center, Map map, float radius)
        {
            var total = 0;
            var plantable = 0;
            foreach (var cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                total++;
                if (ViviaFieldGenerator.CanSpawnViviaAt(cell, map, VVThingDefOf.VV_Plant_FireArcaneFlower))
                {
                    plantable++;
                }
            }

            return total == 0 ? 0f : (float)plantable / total;
        }

        // 나중에 실행되는 유적 생성기 등이 꽃밭을 덮어쓰지 못하도록 영역을 예약한다.
        public static void MarkUsed(IntVec3 center, float radius)
        {
            MapGenerator.UsedRects.Add(CellRect.CenteredOn(center, Mathf.CeilToInt(radius)));
        }
    }
}
