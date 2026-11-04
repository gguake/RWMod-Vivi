using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 정찰이 끝날 때 예약되는 정착대. 예약 인시던트는 한 번만 시도되므로 불발도 '발동'으로 처리해 이유를 알리고 유인을 되돌린다.
    public class IncidentWorker_ViviNestSettlers : IncidentWorker
    {
        private const int HoneyPerSettler = 25;
        private const int InitialVivicream = 60;
        private const int InitialViviwax = 80;
        private const int InitialWood = 40;
        private const int InitialFertilizer = 50;

        private const float ColdSeasonTemp = 10f;
        private const float HotSeasonTemp = 28f;

        private static readonly FieldInfo FieldInfo_QueuedIncident_fireTick = AccessTools.Field(typeof(QueuedIncident), "fireTick");

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && parms.faction.IsWanderingViviFaction();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            var faction = parms.faction;

            var lure = FindLure(map, faction);
            if (lure == null)
            {
                Messages.Message(LocalizeString_Message.VV_Message_ViviNestSettlersNoLure.Translate(), MessageTypeDefOf.NeutralEvent);
                BeekeepingUtility.ReleaseWanderingFaction(faction);
                return true;
            }

            if (!BeekeepingUtility.CheckArrivalConditions(map, out var reason))
            {
                Fail(lure, reason);
                return true;
            }

            var report = lure.EvaluateSiteNow();
            if (!report.Acceptable)
            {
                Fail(lure, report.MissingReasons);
                return true;
            }

            if (!BeekeepingUtility.TryFindEntryCellReaching(map, report.bestRoomCell, out var entryCell))
            {
                Fail(lure, LocalizeString_Inspector.VV_Inspector_LureConditionUnreachable.Translate());
                return true;
            }

            var pawns = GenerateSettlers(faction, map, report.SettlerCount);
            foreach (var pawn in pawns)
            {
                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entryCell, map, 5), map);
            }
            GiveInitialStock(map, pawns);

            LordMaker.MakeNewLord(faction, new LordJob_ViviNest(faction, report.bestRoomCell), map, pawns);
            lure.Notify_Settled();

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ViviNestSettlersLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ViviNestSettlers.Translate(faction.Name, pawns.Count),
                LetterDefOf.PositiveEvent,
                new LookTargets(pawns));
            return true;
        }

        private static Building_ViviLure FindLure(Map map, Faction faction)
        {
            var lures = map.listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_ViviLure);
            for (int i = 0; i < lures.Count; ++i)
            {
                if (lures[i] is Building_ViviLure lure && lure.Spawned && lure.ReservedFaction == faction)
                {
                    return lure;
                }
            }

            return null;
        }

        private static void Fail(Building_ViviLure lure, string reason)
        {
            lure.ResetAttraction(releaseFaction: true);
            Messages.Message(LocalizeString_Message.VV_Message_ViviNestSettlersCanceled.Translate(reason), lure, MessageTypeDefOf.NeutralEvent);
        }

        // 로열 1명 + 나머지는 비무장·새총잡이 반반
        private static List<Pawn> GenerateSettlers(Faction faction, Map map, int count)
        {
            var kinds = new List<PawnKindDef> { VVPawnKindDefOf.VV_WanderingRoyalVivi };
            var others = Mathf.Max(1, count - 1);
            for (int i = 0; i < others; ++i)
            {
                kinds.Add(i % 2 == 0 ? VVPawnKindDefOf.VV_WanderingViviWorker : VVPawnKindDefOf.VV_WanderingViviSlinger);
            }

            var pawns = new List<Pawn>();
            foreach (var kind in kinds)
            {
                var request = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, tile: map.Tile, forceGenerateNewPawn: true);
                pawns.Add(PawnGenerator.GeneratePawn(request));
            }

            return pawns;
        }

        // 둥지를 꾸릴 짐을 나눠 든다. 정착하면 둥지 방에 풀어 놓는다.
        private static void GiveInitialStock(Map map, List<Pawn> pawns)
        {
            var stock = new List<Thing>();
            AddStock(stock, VVThingDefOf.VV_FilteredHoney, HoneyPerSettler * pawns.Count);
            AddStock(stock, VVThingDefOf.VV_Vivicream, InitialVivicream);
            AddStock(stock, VVThingDefOf.VV_Viviwax, InitialViviwax);
            AddStock(stock, ThingDefOf.WoodLog, InitialWood);
            AddStock(stock, VVThingDefOf.VV_Fertilizer, InitialFertilizer);

            foreach (var seedDef in InitialSeedDefs(map))
            {
                AddStock(stock, seedDef, 1);
            }

            var index = 0;
            foreach (var thing in stock)
            {
                var pawn = pawns[index++ % pawns.Count];
                if (!pawn.inventory.innerContainer.TryAdd(thing))
                {
                    thing.Destroy();
                }
            }
        }

        private static void AddStock(List<Thing> stock, ThingDef def, int count)
        {
            if (def == null || count <= 0) { return; }

            while (count > 0)
            {
                var thing = ThingMaker.MakeThing(def);
                thing.stackCount = Mathf.Min(count, def.stackLimit);
                count -= thing.stackCount;
                stock.Add(thing);
            }
        }

        // 정착과 생존에 도움되는 마법식물 씨앗: 추우면 잔불꽃, 더우면 서리꽃, 그리고 풍년화
        private static IEnumerable<ThingDef> InitialSeedDefs(Map map)
        {
            var seasonalTemp = map.mapTemperature.SeasonalTemp;
            if (seasonalTemp < ColdSeasonTemp)
            {
                yield return DefDatabase<ThingDef>.GetNamedSilentFail("VV_Seed_EmberBloom");
            }
            else if (seasonalTemp > HotSeasonTemp)
            {
                yield return DefDatabase<ThingDef>.GetNamedSilentFail("VV_Seed_FrostBloom");
            }

            yield return DefDatabase<ThingDef>.GetNamedSilentFail("VV_Seed_Richflower");
        }

        public static void DebugFireNow(Building_ViviLure lure)
        {
            var faction = lure.ReservedFaction;
            foreach (QueuedIncident queued in Find.Storyteller.incidentQueue)
            {
                var firing = queued.FiringIncident;
                if (firing?.def == VVIncidentDefOf.VV_ViviNestSettlers && firing.parms?.faction == faction)
                {
                    FieldInfo_QueuedIncident_fireTick.SetValue(queued, Find.TickManager.TicksGame);
                }
            }
        }
    }
}
