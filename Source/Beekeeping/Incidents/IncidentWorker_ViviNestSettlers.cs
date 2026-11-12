using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public class IncidentWorker_ViviNestSettlers : IncidentWorker
    {
        private const int HoneyPerSettler = 35;
        private const int InitialVivicream = 60;
        private const int InitialViviwax = 80;
        private const int InitialWood = 40;
        private const int InitialFertilizer = 50;

        private const float ColdSeasonTemp = 10f;
        private const int ColdLandEmberCount = 2;
        private const int OutdoorEmitterCount = 2;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && parms.faction.IsWanderingViviFaction();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            var faction = parms.faction;

            var lure = BeekeepingUtility.FindReservingLure(map, faction);
            if (lure == null)
            {
                BeekeepingUtility.ReleaseWanderingFaction(faction);
                return true;
            }

            if (!BeekeepingUtility.CheckArrivalConditions(map, out var reason))
            {
                Fail(lure, reason);
                return true;
            }

            if (!TrySpawnSettlers(lure, faction, out reason))
            {
                Fail(lure, reason);
            }

            return true;
        }

        public static bool TrySpawnSettlers(Building_ViviLure lure, Faction faction, out string reason)
        {
            var map = lure.Map;
            var report = lure.EvaluateSiteNow();
            if (!report.Acceptable)
            {
                reason = report.MissingReasons;
                return false;
            }

            if (!BeekeepingUtility.TryFindEntryCellReaching(map, report.bestRoomCell, out var entryCell))
            {
                reason = LocalizeString_Inspector.VV_Inspector_LureConditionUnreachable.Translate();
                return false;
            }

            var pawns = GenerateSettlers(faction, map, report.SettlerCount);
            foreach (var pawn in pawns)
            {
                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entryCell, map, 5), map);
            }
            GiveInitialStock(map, pawns);

            var nest = new LordJob_ViviNest(faction, report.bestRoomCell);
            LordMaker.MakeNewLord(faction, nest, map, pawns);
            MarkInitialStockOwned(nest, pawns);
            
            lure.ResetAttraction(releaseFaction: false);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ViviNestSettlersLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ViviNestSettlers.Translate(faction.Name, pawns.Count),
                LetterDefOf.PositiveEvent,
                new LookTargets(pawns));

            reason = null;
            return true;
        }

        public static void DebugSettleNow(Building_ViviLure lure)
        {
            var faction = BeekeepingUtility.CreateWanderingFaction();
            lure.Notify_ScoutVisitStarted(faction);
            if (!TrySpawnSettlers(lure, faction, out var reason))
            {
                lure.ResetAttraction(releaseFaction: true);
                Messages.Message(reason, lure, MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        private static void Fail(Building_ViviLure lure, string reason)
        {
            lure.ResetAttraction(releaseFaction: true);
            Messages.Message(LocalizeString_Message.VV_Message_ViviNestSettlersCanceled.Translate(reason), lure, MessageTypeDefOf.NeutralEvent);
        }

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
                var request = new PawnGenerationRequest(kind, faction, PawnGenerationContext.NonPlayer, tile: map.Tile, forceGenerateNewPawn: true, mustBeCapableOfViolence: kind.isFighter);
                var pawn = PawnGenerator.GeneratePawn(request);
                BeekeepingUtility.RemoveRandomGenes(pawn);
                BeekeepingUtility.RemoveChemicalDependencies(pawn);
                pawns.Add(pawn);
            }

            return pawns;
        }

        private static void GiveInitialStock(Map map, List<Pawn> pawns)
        {
            var stock = new List<Thing>();
            BeekeepingUtility.MakeStacks(VVThingDefOf.VV_FilteredHoney, HoneyPerSettler * pawns.Count, stock);
            BeekeepingUtility.MakeStacks(VVThingDefOf.VV_Vivicream, InitialVivicream, stock);
            BeekeepingUtility.MakeStacks(VVThingDefOf.VV_Viviwax, InitialViviwax, stock);
            BeekeepingUtility.MakeStacks(ThingDefOf.WoodLog, InitialWood, stock);
            BeekeepingUtility.MakeStacks(VVThingDefOf.VV_Fertilizer, InitialFertilizer, stock);

            foreach (var plantDef in InitialPlantDefs(map))
            {
                stock.Add(ThingMaker.MakeThing(plantDef).MakeMinified());
            }
            for (int i = 0; i < OutdoorEmitterCount; ++i)
            {
                stock.Add(ThingMaker.MakeThing(VVThingDefOf.VV_ManaEmitter).MakeMinified());
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

        private static void MarkInitialStockOwned(LordJob_ViviNest nest, List<Pawn> pawns)
        {
            foreach (var pawn in pawns)
            {
                var inventory = pawn.inventory.innerContainer;
                for (int i = 0; i < inventory.Count; ++i)
                {
                    nest.MarkOwned(inventory[i]);
                }
            }
        }

        private static IEnumerable<ThingDef> InitialPlantDefs(Map map)
        {
            if (GenTemperature.MinTemperatureAtTile(map.Tile) < ColdSeasonTemp)
            {
                for (int i = 0; i < ColdLandEmberCount; ++i)
                {
                    yield return VVThingDefOf.VV_EmberBloom;
                }
            }

            yield return VVThingDefOf.VV_Richflower;
        }
    }
}
