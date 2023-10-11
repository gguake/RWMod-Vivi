using RimWorld;
using RimWorld.BaseGen;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class GenStep_ViviSettlement : GenStep_Scatterer
    {
        private static readonly IntRange SettlementSizeX = new IntRange(80, 100);
        private const float SettlementSizeRatio = 11f / 17f;

        public override int SeedPart => 2023100908;

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

            int minX = SettlementSizeX.min;
            int minZ = Mathf.CeilToInt(SettlementSizeX.min * SettlementSizeRatio);
            if (!new CellRect(c.x - minX / 2, c.z - minZ / 2, minX, minZ).FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z)))
            {
                return false;
            }

            return true;
        }

        protected override void ScatterAt(IntVec3 c, Map map, GenStepParams parms, int stackCount = 1)
        {
            int sizeX = SettlementSizeX.RandomInRange;
            int sizeZ = Mathf.CeilToInt(sizeX * SettlementSizeRatio);

            var rect = new CellRect(c.x - sizeX / 2, c.z - sizeZ / 2, sizeX, sizeZ);
            var faction = ((map.ParentFaction != null && map.ParentFaction != Faction.OfPlayer) ? map.ParentFaction : Find.FactionManager.RandomEnemyFaction());
            rect.ClipInsideMap(map);

            var resolveParams = default(ResolveParams);
            resolveParams.rect = rect;
            resolveParams.faction = faction;
            BaseGen.globalSettings.map = map;
            BaseGen.globalSettings.minBuildings = 1;
            BaseGen.globalSettings.minBarracks = 1;
            BaseGen.symbolStack.Push("vv_vivi_settlement", resolveParams);

            BaseGen.Generate();
        }
    }
}
