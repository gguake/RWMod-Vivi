using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class GenStep_ViviAncientRuins : GenStep
    {
        public override int SeedPart => 2026111601;

        public override void Generate(Map map, GenStepParams parms)
        {
            var ruins = ViviEndingQuest.Current?.Ruins;
            var investigated = ruins?.Investigated ?? 0;
            var hexagonSolved = ruins?.HexagonSolved ?? false;
            var coreExtracted = ruins?.CoreExtracted ?? false;

            var origin = map.Center;
            var usedRects = MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects");
            usedRects?.Add(RuinLayoutUtility.FootprintBounds(origin).ExpandedBy(8).ClipInsideMap(map));

            var seed = Gen.HashCombineInt(Find.World?.info?.Seed ?? 0, map.Tile.tileId);
            var comp = map.GetComponent<MapComponent_ViviAncientRuins>();
            comp?.InitializeFromGenStep(origin, seed, investigated, hexagonSolved, coreExtracted);
        }

        public static ThingDef FlowerDef(int index)
        {
            switch (index)
            {
                case 1: return VVThingDefOf.VV_AncientRuinFairyFlowerI;
                case 2: return VVThingDefOf.VV_AncientRuinFairyFlowerII;
                case 3: return VVThingDefOf.VV_AncientRuinFairyFlowerIII;
                case 4: return VVThingDefOf.VV_AncientRuinFairyFlowerIV;
                default: return null;
            }
        }
    }

    public class GenStep_ViviAncientRuinsPostFog : GenStep
    {
        public override int SeedPart => 2026111602;

        public override void Generate(Map map, GenStepParams parms)
        {
            var comp = map.GetComponent<MapComponent_ViviAncientRuins>();
            comp?.PostMapGenerationCleanup();
        }
    }
}
