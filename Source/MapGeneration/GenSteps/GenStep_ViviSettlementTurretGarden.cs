using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace VVRace
{
    public class GenStep_ViviSettlementTurretGarden : GenStep_Scatterer
    {
        // 밭 하나가 차지하는 정사각 영역의 한 변
        private const int AreaSize = 11;

        public string symbol = "vv_turret_garden";
        public int seedPart = 2023101223;

        // 적으면 가운데 fertileSize 칸 정사각 영역이 모두 이 식물이 자랄 만큼 비옥해야 한다
        public ThingDef fertileFor;
        public int fertileSize = 7;

        public override int SeedPart => seedPart;

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

            var cellRect = CellRect.CenteredOn(c, AreaSize, AreaSize);
            if (!cellRect.FullyContainedWithin(new CellRect(0, 0, map.Size.x, map.Size.z)))
            {
                return false;
            }

            foreach (var cell in cellRect)
            {
                if (!cell.Walkable(map) || cell.Roofed(map) || IsOccupied(cell, map))
                {
                    return false;
                }
            }

            if (fertileFor != null)
            {
                foreach (var cell in CellRect.CenteredOn(c, fertileSize, fertileSize))
                {
                    if (map.fertilityGrid.FertilityAt(cell) < fertileFor.plant.fertilityMin)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // 건물이나 다른 밭의 꽃이 있는 칸에는 밭을 겹쳐 만들지 않는다
        private static bool IsOccupied(IntVec3 cell, Map map)
        {
            foreach (var thing in cell.GetThingList(map))
            {
                if (thing.def.category == ThingCategory.Building)
                {
                    return true;
                }

                // 그 바이옴에 저절로 자라지 않는 꽃은 다른 밭에 심은 것
                if (thing is Plant plant && plant.def.plant.purpose == PlantPurpose.Beauty && map.Biome.CommonalityOfPlant(plant.def) <= 0f)
                {
                    return true;
                }
            }

            return false;
        }

        protected override void ScatterAt(IntVec3 c, Map map, GenStepParams parms, int stackCount = 1)
        {
            var rect = CellRect.CenteredOn(c, AreaSize, AreaSize);
            var faction = ((map.ParentFaction != null && map.ParentFaction != Faction.OfPlayerSilentFail) ? map.ParentFaction : Find.FactionManager.RandomEnemyFaction());
            rect.ClipInsideMap(map);

            var resolveParams = default(ResolveParams);
            resolveParams.rect = rect;
            resolveParams.faction = faction;
            BaseGen.globalSettings.map = map;
            BaseGen.symbolStack.Push(symbol, resolveParams);

            BaseGen.Generate();
        }
    }
}
