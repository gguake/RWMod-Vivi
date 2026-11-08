using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class TileMutatorWorker_ArcanePlantHabitat : TileMutatorWorker_ViviLandmark
    {
        private const float MinViviaPlantableRatio = 0.4f;

        public TileMutatorWorker_ArcanePlantHabitat(TileMutatorDef def) : base(def)
        {
        }

        private ArcanePlantHabitatExtension Extension => def.GetModExtension<ArcanePlantHabitatExtension>();

        public override string GetDescription(PlanetTile tile)
        {
            return def.description.Formatted(GetPlantKinds(tile).Select(kind => kind.label).ToCommaList(useAnd: true));
        }

        // 월드맵 설명과 실제 맵이 같은 종을 쓰도록 타일 시드로 고른다.
        public List<ThingDef> GetPlantKinds(PlanetTile tile)
        {
            var extension = Extension;
            if (extension == null || extension.plantKinds.NullOrEmpty()) { return new List<ThingDef>(); }

            Rand.PushState(tile.GetHashCode());
            try
            {
                return extension.plantKinds.InRandomOrder().Take(extension.plantKindCount).ToList();
            }
            finally
            {
                Rand.PopState();
            }
        }

        public override void GenerateLandmark(Map map)
        {
            var extension = Extension;
            var kinds = GetPlantKinds(map.Tile);
            if (kinds.Count == 0) { return; }

            var manaComponent = map.GetManaComponent();
            var clusterCount = Mathf.Max(kinds.Count, ViviLandmarkUtility.CountForMap(map, extension));
            var clusterKinds = new List<ThingDef>(kinds);
            while (clusterKinds.Count < clusterCount)
            {
                clusterKinds.Add(kinds.RandomElement());
            }

            var centers = ViviLandmarkUtility.FindSpacedCells(map, clusterCount, extension.minSpacing, c => IsValidClusterCenter(c, map, extension));
            for (int i = 0; i < centers.Count; ++i)
            {
                if (SpawnCluster(map, centers[i], clusterKinds[i], extension) == 0) { continue; }

                // 마법 식물이 자라는 자리에서 마나가 솟아오른다.
                manaComponent?.AddManaVein(centers[i]);

                var radius = extension.fieldRadiusRange.RandomInRange;
                ViviaFieldGenerator.Generate(map, centers[i], radius, extension.fieldDensity, extension.fieldColorCountRange.RandomInRange);
                ViviLandmarkUtility.MarkUsed(centers[i], ViviaFieldGenerator.FieldExtent(radius));
            }
        }

        private static int SpawnCluster(Map map, IntVec3 center, ThingDef kind, ArcanePlantHabitatExtension extension)
        {
            var count = extension.plantsPerClusterRange.RandomInRange;
            var placed = new List<IntVec3>();
            foreach (var cell in GenRadial.RadialCellsAround(center, extension.clusterRadius, true).InRandomOrder())
            {
                if (placed.Count >= count) { break; }
                if (placed.Any(p => p.DistanceTo(cell) < extension.plantSpacing)) { continue; }
                if (!CanSpawnArcanePlantAt(cell, map, kind)) { continue; }

                ViviaFieldGenerator.ClearReplaceableWildPlant(cell, map);
                GenSpawn.Spawn(ThingMaker.MakeThing(kind), cell, map);
                placed.Add(cell);
            }

            return placed.Count;
        }

        private static bool IsValidClusterCenter(IntVec3 c, Map map, ArcanePlantHabitatExtension extension)
        {
            var kind = extension.plantKinds[0];
            if (ViviLandmarkUtility.IsInEdgeMargin(c, map, extension.edgeMargin)) { return false; }
            if (!CanSpawnArcanePlantAt(c, map, kind)) { return false; }

            var fieldExtent = ViviaFieldGenerator.FieldExtent(extension.fieldRadiusRange.max);
            if (!ViviLandmarkUtility.IsAreaUnused(c, fieldExtent)) { return false; }

            var plantableCells = GenRadial.RadialCellsAround(c, extension.clusterRadius, true).Count(cell => CanSpawnArcanePlantAt(cell, map, kind));
            if (plantableCells < extension.plantsPerClusterRange.max) { return false; }
            if (ViviLandmarkUtility.ViviaPlantableRatioAround(c, map, extension.fieldRadiusRange.min) < MinViviaPlantableRatio) { return false; }

            return ViviLandmarkUtility.IsClearOfStructures(c, fieldExtent, map);
        }

        private static bool CanSpawnArcanePlantAt(IntVec3 c, Map map, ThingDef kind)
        {
            if (!c.InBounds(map) || c.Roofed(map) || !c.Standable(map)) { return false; }
            if (c.IsPolluted(map) || map.fertilityGrid.FertilityAt(c) <= 0f) { return false; }

            foreach (var thing in c.GetThingList(map))
            {
                if (!(thing is Plant plant) || !ViviaFieldGenerator.IsReplaceableWildPlant(plant)) { return false; }
            }

            return c.SupportsStructureType(map, kind.terrainAffordanceNeeded);
        }
    }
}
