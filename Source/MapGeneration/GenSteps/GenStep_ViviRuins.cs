using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class GenStep_ViviRuins : GenStep_Scatterer
    {
        private static readonly int SettlementSizeX = 45;
        private static readonly int SettlementSizeZ = 45;

        public override int SeedPart => 2024041311;

        protected override bool CanScatterAt(IntVec3 c, Map map)
        {
            if (!base.CanScatterAt(c, map))
            {
                return false;
            }

            if (!c.Standable(map))
            {
                return false;
            }
            if (c.Roofed(map))
            {
                return false;
            }
            if (!map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)))
            {
                return false;
            }

            var cellRect = CellRect.CenteredOn(c, SettlementSizeX, SettlementSizeZ);
            if (!cellRect.FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z)))
            {
                return false;
            }

            var altarSize = VVThingDefOf.VV_DreamumAltar.Size;
            var altarCellRect = CellRect.CenteredOn(c, altarSize.x, altarSize.z);
            foreach (var cell in altarCellRect)
            {
                if (cell.GetTerrain(map).IsWater || cell.Impassable(map)) { return false; }
            }

            return true;
        }

        protected override void ScatterAt(IntVec3 c, Map map, GenStepParams parms, int count = 1)
        {
            int sizeX = SettlementSizeX;
            int sizeZ = SettlementSizeZ;

            var rect = CellRect.CenteredOn(c, sizeX, sizeZ);
            rect.ClipInsideMap(map);

            var resolveParams = default(ResolveParams);
            resolveParams.rect = rect;
            resolveParams.faction = null;
            BaseGen.globalSettings.map = map;
            BaseGen.symbolStack.Push("vv_vivi_ruins", resolveParams);

            BaseGen.Generate();
        }
    }
}
