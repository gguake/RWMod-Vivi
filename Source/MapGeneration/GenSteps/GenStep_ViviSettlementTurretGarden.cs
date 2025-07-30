using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class GenStep_ViviSettlementTurretGarden : GenStep_Scatterer
    {
        public override int SeedPart => 2023101223;

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
            if (!c.SupportsStructureType(map, VVThingDefOf.VV_ArcanePlantPot.terrainAffordanceNeeded))
            {
                return false;
            }

            if (!map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)))
            {
                return false;
            }

            var cellRect = CellRect.CenteredOn(c, 7, 7);
            if (!cellRect.FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z)))
            {
                return false;
            }

            foreach (var cell in cellRect)
            {
                if (!c.Standable(map) || cell.Roofed(map))
                {
                    return false;
                }
            }

            return true;
        }

        protected override void ScatterAt(IntVec3 c, Map map, GenStepParams parms, int stackCount = 1)
        {
            var rect = CellRect.CenteredOn(c, 7, 7);
            var faction = ((map.ParentFaction != null && map.ParentFaction != Faction.OfPlayerSilentFail) ? map.ParentFaction : Find.FactionManager.RandomEnemyFaction());
            rect.ClipInsideMap(map);

            var resolveParams = default(ResolveParams);
            resolveParams.rect = rect;
            resolveParams.faction = faction;
            BaseGen.globalSettings.map = map;
            BaseGen.symbolStack.Push("vv_turret_garden", resolveParams);

            BaseGen.Generate();
        }
    }
}
