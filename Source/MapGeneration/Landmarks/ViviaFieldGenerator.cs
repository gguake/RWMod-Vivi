using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Noise;

namespace VVRace
{
    // 맵 생성 시 비비아 꽃밭을 만든다. 가장자리는 노이즈로 불규칙하게, 색은 몇 개의 무리로 나눈다.
    public static class ViviaFieldGenerator
    {
        // 자연 발생(ArcanePlantUtility.TrySpawnViviFlower)과 같은 비옥도 기준
        private const float MinFertility = 0.5f;

        private const float EdgeNoiseFrequency = 0.13f;
        private const float EdgeNoiseStrength = 0.3f;
        private const float DenseCoreRatio = 0.6f;

        private const float ColorWarpFrequency = 0.15f;
        private const float ColorWarpStrength = 3.5f;
        private const float StrayColorChance = 0.06f;
        private static readonly IntRange ColorPatchCountRange = new IntRange(3, 5);

        private static readonly FloatRange GrowthRange = new FloatRange(0.55f, 1f);
        private const float MaxAgeLifespanRatio = 0.6f;

        // 가장자리 노이즈 때문에 꽃밭은 반경보다 조금 더 넓게 퍼질 수 있다.
        public static float FieldExtent(float radius) => radius * (1f + EdgeNoiseStrength);

        public static int Generate(Map map, IntVec3 center, float radius, float density, int colorCount)
        {
            var allColors = ArcanePlantUtility.ViviFlowerDefs.ToList();
            var palette = allColors.InRandomOrder().Take(Mathf.Clamp(colorCount, 1, allColors.Count)).ToList();

            // 색 무리 중심마다 팔레트 색을 돌아가며 배정한다.
            var patchCenters = new List<IntVec3>();
            var patchColors = new List<ThingDef>();
            var patchCount = ColorPatchCountRange.RandomInRange;
            var patchCellCount = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < patchCount; ++i)
            {
                patchCenters.Add(center + GenRadial.RadialPattern[Rand.Range(0, patchCellCount)]);
                patchColors.Add(palette[i % palette.Count]);
            }

            var edgeSeed = Rand.Int;
            var warpSeed = Rand.Int;
            var spawned = 0;
            foreach (var cell in GenRadial.RadialCellsAround(center, FieldExtent(radius), true))
            {
                var edgeNoise = Mathf.Clamp((float)Perlin.GetValue(cell.x, 0.0, cell.z, EdgeNoiseFrequency, edgeSeed, octaveCount: 2), -1f, 1f);
                var t = cell.DistanceTo(center) / radius + edgeNoise * EdgeNoiseStrength;
                if (t >= 1f) { continue; }
                if (!Rand.Chance(density * (1f - Mathf.SmoothStep(DenseCoreRatio, 1f, t)))) { continue; }

                var flowerDef = Rand.Chance(StrayColorChance) ? allColors.RandomElement() : PickPatchColor(cell, patchCenters, patchColors, warpSeed);
                if (!CanSpawnViviaAt(cell, map, flowerDef)) { continue; }

                SpawnVivia(flowerDef, cell, map);
                spawned++;
            }

            return spawned;
        }

        public static bool CanSpawnViviaAt(IntVec3 c, Map map, ThingDef flowerDef)
        {
            if (!c.InBounds(map) || c.Roofed(map)) { return false; }
            if (c.GetFertility(map) < MinFertility) { return false; }
            if (c.GetCover(map) != null || c.GetEdifice(map) != null) { return false; }
            if (c.GetFirstItem(map) != null || c.IsPolluted(map)) { return false; }

            var plant = c.GetPlant(map);
            if (plant != null && !IsReplaceableWildPlant(plant)) { return false; }

            return flowerDef.CanEverPlantAt(c, map, canWipePlantsExceptTree: true);
        }

        // 맵 생성 시 꽃밭/군락 자리에 이미 난 풀·덤불은 걷어 내고, 나무는 남긴다.
        public static bool IsReplaceableWildPlant(Plant plant)
        {
            return !plant.def.plant.IsTree;
        }

        public static void ClearReplaceableWildPlant(IntVec3 c, Map map)
        {
            var plant = c.GetPlant(map);
            if (plant != null && IsReplaceableWildPlant(plant))
            {
                plant.Destroy();
            }
        }

        private static ThingDef PickPatchColor(IntVec3 cell, List<IntVec3> patchCenters, List<ThingDef> patchColors, int warpSeed)
        {
            // 칸 좌표를 노이즈로 비틀어 색 경계가 곧게 나뉘지 않게 한다.
            var x = cell.x + (float)Perlin.GetValue(cell.x, 0.0, cell.z, ColorWarpFrequency, warpSeed, octaveCount: 2) * ColorWarpStrength;
            var z = cell.z + (float)Perlin.GetValue(cell.x, 0.0, cell.z, ColorWarpFrequency, warpSeed + 1, octaveCount: 2) * ColorWarpStrength;

            var nearest = 0;
            var nearestDistSq = float.MaxValue;
            for (int i = 0; i < patchCenters.Count; ++i)
            {
                var dx = x - patchCenters[i].x;
                var dz = z - patchCenters[i].z;
                var distSq = dx * dx + dz * dz;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = i;
                }
            }

            return patchColors[nearest];
        }

        private static void SpawnVivia(ThingDef flowerDef, IntVec3 cell, Map map)
        {
            var plant = (Plant)ThingMaker.MakeThing(flowerDef);
            plant.Growth = GrowthRange.RandomInRange;
            if (flowerDef.plant.LimitedLifespan)
            {
                // 같은 날 한꺼번에 시들지 않도록 나이를 흩어 놓는다.
                plant.Age = Rand.Range(0, (int)(flowerDef.plant.LifespanTicks * MaxAgeLifespanRatio));
            }

            ClearReplaceableWildPlant(cell, map);
            GenSpawn.Spawn(plant, cell, map);
        }
    }
}
