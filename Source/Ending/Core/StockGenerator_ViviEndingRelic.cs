using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 비비 연방 상인의 고대 호박 코어 재고(Patches/Ending_Traders.xml).
    // 연방의 서신을 받은 뒤, 엔딩이 끝나지 않았고 플레이어가 코어나 완성된 제단을 갖고 있지 않을 때만 판다.
    public class StockGenerator_ViviEndingRelic : StockGenerator_SingleDef
    {
        private static readonly List<Thing> tmpThings = new List<Thing>();

        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            if (!CanOfferRelic()) { yield break; }

            foreach (var thing in base.GenerateThings(forTile, faction))
            {
                yield return thing;
            }
        }

        private static bool CanOfferRelic()
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null || !quest.IsAltarUnlocked) { return false; }
            if (quest.Stage.IsFinishedStage()) { return false; }

            return !PlayerHasAmberCoreOrAltar();
        }

        // 플레이어가 호박 코어나 완성된 제단을 갖고 있는지. 코어 재전달 판정에도 쓴다.
        // 맵에 놓인 코어뿐 아니라 맵 안의 보관처(폰의 소지품·운반 중인 물건, 건설 중인 제단 틀, 수송 포드, 떨어지는 중인 드롭포드 등)에 든 코어,
        // 캐러밴·비행 중인 수송 포드·중력선에 실린 코어도 센다. 제단 청사진이나 코어가 들어가지 않은 제단 틀은 세지 않는다
        // (코어를 잃었는데 청사진·틀 때문에 재전달과 연방 상인 재고가 막히지 않도록).
        internal static bool PlayerHasAmberCoreOrAltar()
        {
            var coreDef = EndingCoreDefOf.VV_AncientAmberCore;
            var altarDef = EndingCoreDefOf.VV_DreamumAltar;

            try
            {
                foreach (var map in Find.Maps)
                {
                    if (altarDef != null && AnyPlayerThing(map.listerThings.ThingsOfDef(altarDef)))
                    {
                        return true;
                    }

                    if (coreDef != null)
                    {
                        ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForDef(coreDef), tmpThings, passCheck: CanHoldPlayerCore);
                        if (tmpThings.Count > 0)
                        {
                            return true;
                        }
                    }
                }

                foreach (var worldObject in Find.WorldObjects.AllWorldObjects)
                {
                    if (worldObject.Faction != Faction.OfPlayer) { continue; }

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

                return false;
            }
            finally
            {
                tmpThings.Clear();
            }
        }

        // 지나가는 궤도 상인과 방문 중인 비적대 세력(상단·방문객)이 든 물건은 플레이어 것으로 치지 않는다.
        // 플레이어 폰·짐 동물이 든 코어와 적대 세력이 훔쳐 가는 중인 코어(맵에 있는 동안은 되찾을 수 있다)는 센다.
        private static bool CanHoldPlayerCore(IThingHolder holder)
        {
            if (holder is PassingShip) { return false; }

            if (holder is Pawn pawn && pawn.Faction != null && pawn.Faction != Faction.OfPlayer && !pawn.Faction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            return true;
        }

        private static bool HoldsCoreOrAltar(IThingHolder holder, ThingDef coreDef, ThingDef altarDef)
        {
            ThingOwnerUtility.GetAllThingsRecursively(holder, tmpThings, passCheck: CanHoldPlayerCore);
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

            return (coreDef != null && thing.def == coreDef) || (altarDef != null && thing.def == altarDef && thing.Faction == Faction.OfPlayer);
        }

        private static bool AnyPlayerThing(List<Thing> things)
        {
            for (int i = 0; i < things.Count; ++i)
            {
                if (things[i].Faction == Faction.OfPlayer)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
