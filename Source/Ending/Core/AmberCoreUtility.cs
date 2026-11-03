using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 통신기 대화를 열 때만 조회하므로 코어를 놓치지 않도록 월드 폰의 소지품까지 확인한다.
    public static class AmberCoreUtility
    {
        private static readonly List<Thing> tmpThings = new List<Thing>();

        // 코어가 재료로 소모된 완성 제단도 보유 코어로 센다. 청사진과 코어 투입 전의 틀은 제외한다.
        // 진행 중인 의뢰만으로 재의뢰가 차단되므로 퀘스트 부품에서 보상 대기 중인 코어는 세지 않는다.
        public static bool AnyAmberCoreExists()
        {
            var coreDef = VVThingDefOf.VV_AncientAmberCore;
            var altarDef = VVThingDefOf.VV_EverflowerAltar;

            try
            {
                foreach (var map in Find.Maps)
                {
                    if (altarDef != null && map.listerThings.ThingsOfDef(altarDef).Count > 0)
                    {
                        return true;
                    }

                    if (coreDef != null)
                    {
                        ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForDef(coreDef), tmpThings);
                        if (tmpThings.Count > 0)
                        {
                            return true;
                        }
                    }
                }

                foreach (var worldObject in Find.WorldObjects.AllWorldObjects)
                {
                    if (worldObject is Caravan || worldObject is TravellingTransporters)
                    {
                        if (HoldsCoreOrAltar((IThingHolder)worldObject, coreDef, altarDef))
                        {
                            return true;
                        }
                    }
                    else if (worldObject is Gravship gravship)
                    {
                        foreach (var thing in gravship.Things)
                        {
                            if (IsCoreOrAltar(thing, coreDef, altarDef) || (thing is IThingHolder holder && HoldsCoreOrAltar(holder, coreDef, altarDef)))
                            {
                                return true;
                            }
                        }

                        foreach (var pawn in gravship.Pawns)
                        {
                            if (HoldsCoreOrAltar(pawn, coreDef, altarDef))
                            {
                                return true;
                            }
                        }
                    }
                }

                var worldPawns = Find.WorldPawns.AllPawnsAliveOrDead;
                for (int i = 0; i < worldPawns.Count; ++i)
                {
                    if (HoldsCoreOrAltar(worldPawns[i], coreDef, altarDef))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                tmpThings.Clear();
            }
        }

        private static bool HoldsCoreOrAltar(IThingHolder holder, ThingDef coreDef, ThingDef altarDef)
        {
            ThingOwnerUtility.GetAllThingsRecursively(holder, tmpThings);
            for (int i = 0; i < tmpThings.Count; ++i)
            {
                if (IsCoreOrAltar(tmpThings[i], coreDef, altarDef))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCoreOrAltar(Thing thing, ThingDef coreDef, ThingDef altarDef)
        {
            if (thing == null) { return false; }

            return (coreDef != null && thing.def == coreDef) || (altarDef != null && thing.def == altarDef);
        }
    }
}
