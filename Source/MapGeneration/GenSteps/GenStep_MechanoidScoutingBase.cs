//using RimWorld;
//using RimWorld.BaseGen;
//using Verse;

//namespace VVRace
//{
//    public class GenStep_MechanoidScoutingBase : GenStep_ScattererBestFit
//    {
//        public override int SeedPart => 84642187;

//        protected override IntVec2 Size => SymbolResolver_MechanoidScoutingBase.Size;

//        public override bool CollisionAt(IntVec3 cell, Map map)
//        {
//            TerrainDef terrain = cell.GetTerrain(map);
//            if (terrain != null && (terrain.IsWater || terrain.IsRoad))
//            {
//                return true;
//            }

//            var thingList = cell.GetThingList(map);
//            for (int i = 0; i < thingList.Count; i++)
//            {
//                if (thingList[i].def.IsBuildingArtificial || (thingList[i].def.building != null && thingList[i].def.building.isNaturalRock))
//                {
//                    return true;
//                }
//            }
//            return false;
//        }

//        public override void Generate(Map map, GenStepParams parms)
//        {
//            count = 1;
//            nearMapCenter = true;

//            base.Generate(map, parms);
//        }

//        protected override bool TryFindScatterCell(Map map, out IntVec3 result)
//        {
//            if (!base.TryFindScatterCell(map, out result))
//            {
//                result = map.Center;
//            }
//            return true;
//        }

//        protected override void ScatterAt(IntVec3 c, Map map, GenStepParams genStepParams, int stackCount = 1)
//        {
//            var sitePartParams = genStepParams.sitePart.parms;
//            ResolveParams resolveParams = default;
//            resolveParams.sitePart = genStepParams.sitePart;
//            resolveParams.faction = genStepParams.sitePart.site.Faction;
//            resolveParams.triggerSecuritySignal = sitePartParams.triggerSecuritySignal;
//            resolveParams.threatPoints = sitePartParams.threatPoints;
//            resolveParams.rect = CellRect.CenteredOn(c, Size.x, Size.z);
//            BaseGen.globalSettings.map = map;
//            BaseGen.symbolStack.Push("vv_mechanoid_scouting_base", resolveParams);
//            BaseGen.Generate();
//        }
//    }
//}
