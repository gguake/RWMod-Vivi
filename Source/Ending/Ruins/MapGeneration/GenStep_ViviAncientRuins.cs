using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    // 시험용 임시 배치: 맵 가운데 줄에 요정꽃 1~4와 중심 제단을 둔다. 실제 유적 구조·말벌 군락은 유적 맵 작업에서 이 단계를 대체한다.
    public class GenStep_ViviAncientRuins : GenStep
    {
        private const int Spacing = 8;
        private const int ClearRadius = 3;

        public override int SeedPart => 2026111601;

        public override void Generate(Map map, GenStepParams parms)
        {
            var ruins = ViviEndingQuest.Current?.Ruins;
            var investigated = ruins?.Investigated ?? 0;

            var center = map.Center;
            var altarCell = new IntVec3(center.x, 0, center.z + Spacing * 2);
            if (!(ruins?.CoreExtracted ?? false))
            {
                SpawnAt(VVThingDefOf.VV_AncientRuinAltar, altarCell, map);
            }

            for (int index = 1; index <= ViviAncientRuins.EchoCount; ++index)
            {
                if (index <= investigated) { continue; }

                var cell = new IntVec3(center.x + (index - 1 - ViviAncientRuins.EchoCount / 2) * Spacing, 0, center.z);
                SpawnAt(FlowerDef(index), cell, map);
            }
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

        private static void SpawnAt(ThingDef def, IntVec3 cell, Map map)
        {
            if (def == null) { return; }

            cell = cell.ClampInsideMap(map);
            foreach (var c in GenRadial.RadialCellsAround(cell, ClearRadius + def.size.x / 2f, true))
            {
                if (!c.InBounds(map)) { continue; }

                foreach (var thing in c.GetThingList(map).ToArray())
                {
                    if (thing.def.destroyable && !(thing is Pawn))
                    {
                        thing.Destroy();
                    }
                }

                if (!c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Medium))
                {
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.Soil);
                }
                map.roofGrid.SetRoof(c, null);
            }

            GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map, WipeMode.Vanish);
        }
    }
}
