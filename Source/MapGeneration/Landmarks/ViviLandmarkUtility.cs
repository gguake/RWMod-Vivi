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

        // 비비아 꽃이 자라는 환경인지 본다. 모드 바이옴에도 맞도록 바이옴 목록 대신 바이옴 성질과 기온으로 판정한다.
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

        // 서로 minSpacing 이상 떨어진 칸을 count개까지 찾는다. 맞는 칸이 모자라면 찾은 만큼만 돌려준다.
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

        // 다른 생성 단계가 예약한 영역(UsedRects)과 겹치지 않는지
        public static bool IsAreaUnused(IntVec3 center, float radius)
        {
            var area = CellRect.CenteredOn(center, Mathf.CeilToInt(radius));
            return !MapGenerator.UsedRects.Any(rect => rect.Overlaps(area));
        }

        // 꽃밭 자리에 유적 잔해(돌무더기, 잔해 오물, 벽 등)가 없는지 본다. 자연 암벽은 괜찮다.
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

        // 뒤에 실행되는 구조물 생성기(유적 등)가 꽃밭을 덮어쓰지 않게 영역을 등록한다.
        public static void MarkUsed(IntVec3 center, float radius)
        {
            MapGenerator.UsedRects.Add(CellRect.CenteredOn(center, Mathf.CeilToInt(radius)));
        }
    }
}
