using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 고대 호박 코어는 게임에 하나만 있을 수 있고, 코어가 파괴되기 전까지는 통신기 의뢰(EndingAmberCore)를 다시 받을 수 없다.
    // 코어가 게임 어딘가에 남아 있는지 훑는다. 통신기 대화를 열 때만 부르므로 월드 폰까지 모두 훑는다.
    public static class AmberCoreUtility
    {
        private static readonly List<Thing> tmpThings = new List<Thing>();

        // 코어나 제단이 남아 있는지. 보유자와 무관하게 센다:
        // 맵 위의 모든 보관처(바닥·폰 소지품·적·시체·건설 중인 제단 틀·드롭포드·수송 포드 등)와 맵의 제단,
        // 월드의 캐러밴·비행 중인 수송 포드·중력선, 월드 폰(코어를 약탈해 간 적 포함)의 소지품.
        // 제단은 코어를 품은 것으로 친다(건설하면 코어가 소모되고, 해체하면 코어가 돌아온다). 제단 청사진과 코어가 들어가지 않은 제단 틀은 세지 않는다.
        // 맵이 사라지면 그 맵의 코어·제단도 사라진 것으로 친다.
        // 의뢰 퀘스트 보상으로 대기 중인 코어(퀘스트 부품 안)는 세지 않는다(의뢰 퀘스트가 진행 중이면 그것만으로 의뢰를 막는다).
        public static bool AnyAmberCoreExists()
        {
            var coreDef = EndingCoreDefOf.VV_AncientAmberCore;
            var altarDef = EndingCoreDefOf.VV_DreamumAltar;

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

                // 캐러밴 폰도 월드 폰이라 겹쳐 세지만 결과에는 영향이 없다.
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
