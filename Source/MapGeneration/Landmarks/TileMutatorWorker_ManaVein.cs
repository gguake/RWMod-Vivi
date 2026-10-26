using RimWorld;
using Verse;

namespace VVRace
{
    // 마나 영맥: 맵의 무작위 지점 몇 곳을 영구 마나 방출 지점으로 등록하고 주변에 비비아 꽃밭을 만든다.
    public class TileMutatorWorker_ManaVein : TileMutatorWorker_ViviLandmark
    {
        private const float FieldCheckRadius = 4f;
        private const float MinPlantableRatio = 0.5f;

        public TileMutatorWorker_ManaVein(TileMutatorDef def) : base(def)
        {
        }

        private ManaVeinExtension Extension => def.GetModExtension<ManaVeinExtension>();

        public override void GenerateLandmark(Map map)
        {
            var extension = Extension;
            var manaComponent = map.GetManaComponent();
            if (extension == null || manaComponent == null) { return; }

            var count = ViviLandmarkUtility.CountForMap(map, extension);
            var veins = ViviLandmarkUtility.FindSpacedCells(map, count, extension.minSpacing, c => IsValidVeinCell(c, map, extension));
            foreach (var vein in veins)
            {
                manaComponent.AddManaVein(vein);

                var radius = extension.fieldRadiusRange.RandomInRange;
                ViviaFieldGenerator.Generate(map, vein, radius, extension.fieldDensity, extension.fieldColorCountRange.RandomInRange);
                ViviLandmarkUtility.MarkUsed(vein, ViviaFieldGenerator.FieldExtent(radius));
            }
        }

        private static bool IsValidVeinCell(IntVec3 c, Map map, ViviLandmarkExtension extension)
        {
            if (ViviLandmarkUtility.IsInEdgeMargin(c, map, extension.edgeMargin)) { return false; }
            if (!c.Standable(map) || c.GetTerrain(map).IsWater) { return false; }
            if (!ViviaFieldGenerator.CanSpawnViviaAt(c, map, VVThingDefOf.VV_Plant_FireArcaneFlower)) { return false; }

            var fieldExtent = ViviaFieldGenerator.FieldExtent(extension.fieldRadiusRange.max);
            if (!ViviLandmarkUtility.IsAreaUnused(c, fieldExtent)) { return false; }
            if (ViviLandmarkUtility.ViviaPlantableRatioAround(c, map, FieldCheckRadius) < MinPlantableRatio) { return false; }

            return ViviLandmarkUtility.IsClearOfStructures(c, fieldExtent, map);
        }
    }
}
