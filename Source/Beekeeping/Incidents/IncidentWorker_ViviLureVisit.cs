using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public enum ViviLureVisitor
    {
        Scouts,
        Bear,
        Hornet,
    }

    public class IncidentWorker_ViviLureVisit : IncidentWorker
    {
        private const float ScoutWeight = 75f;
        private const float BearWeight = 20f;
        private const float HornetWeight = 5f;

        private static readonly IntRange ScoutCountRange = new IntRange(1, 2);
        private static readonly IntRange ScoutVisitTicksRange = new IntRange(4000, 8000);

        private const int HornetStayTicks = GenDate.TicksPerDay;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms)) { return false; }

            var map = (Map)parms.target;
            return TryFindLure(map, out _) && BeekeepingUtility.CheckArrivalConditions(map, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            if (!TryFindLure(map, out var lure)) { return false; }

            var visitor = new List<(ViviLureVisitor, float)>
            {
                (ViviLureVisitor.Scouts, ScoutWeight),
                (ViviLureVisitor.Bear, BearWeight),
                (ViviLureVisitor.Hornet, HornetWeight),
            }.RandomElementByWeight(v => v.Item2).Item1;

            return TryStartVisit(lure, visitor);
        }

        public static bool TryFindLure(Map map, out Building_ViviLure lure)
        {
            lure = null;
            if (map == null || BeekeepingUtility.AnyNestOrAttraction(map)) { return false; }

            var candidates = map.listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_ViviLure);
            var baited = new List<Building_ViviLure>();
            for (int i = 0; i < candidates.Count; ++i)
            {
                if (candidates[i] is Building_ViviLure viviLure && viviLure.Spawned && viviLure.HasBait
                    && BeekeepingUtility.TryFindEntryCellReaching(map, viviLure.Position, out _))
                {
                    baited.Add(viviLure);
                }
            }

            return baited.TryRandomElement(out lure);
        }

        public static bool TryStartVisit(Building_ViviLure lure, ViviLureVisitor visitor)
        {
            switch (visitor)
            {
                case ViviLureVisitor.Scouts:
                    return TryStartScouts(lure);

                case ViviLureVisitor.Bear:
                    var bearKind = lure.Map.mapTemperature.SeasonalTemp < 0f ? VVPawnKindDefOf.Bear_Polar : VVPawnKindDefOf.Bear_Grizzly;
                    return TryStartThief(lure, bearKind);

                case ViviLureVisitor.Hornet:
                    return TryStartHornet(lure);
            }

            return false;
        }

        private static bool TryStartScouts(Building_ViviLure lure)
        {
            var map = lure.Map;
            if (!BeekeepingUtility.TryFindEntryCellReaching(map, lure.Position, out var entryCell)) { return false; }

            var faction = BeekeepingUtility.CreateWanderingFaction();
            var pawns = new List<Pawn>();
            var count = ScoutCountRange.RandomInRange;
            for (int i = 0; i < count; ++i)
            {
                var request = new PawnGenerationRequest(
                    VVPawnKindDefOf.VV_WanderingViviWorker,
                    faction,
                    PawnGenerationContext.NonPlayer,
                    tile: map.Tile,
                    forceGenerateNewPawn: true,
                    mustBeCapableOfViolence: false);
                var pawn = PawnGenerator.GeneratePawn(request);
                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entryCell, map, 4), map);
                pawns.Add(pawn);
            }

            lure.Notify_ScoutVisitStarted(faction);
            LordMaker.MakeNewLord(faction, new LordJob_ViviLureScout(faction, lure, ScoutVisitTicksRange.RandomInRange), map, pawns);

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ViviLureScoutsLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ViviLureScouts.Translate(faction.Name, pawns.Count),
                LetterDefOf.NeutralEvent,
                new LookTargets(pawns.Cast<Thing>().Append(lure)));
            return true;
        }

        private static bool TryStartThief(Building_ViviLure lure, PawnKindDef kindDef)
        {
            var map = lure.Map;
            if (!BeekeepingUtility.TryFindEntryCellReaching(map, lure.Position, out var entryCell)) { return false; }

            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kindDef, null, PawnGenerationContext.NonPlayer, tile: map.Tile));
            GenSpawn.Spawn(pawn, entryCell, map);

            if (pawn.needs?.food != null)
            {
                pawn.needs.food.CurLevel = 0f;
            }

            lure.Notify_ThiefVisitStarted();
            LordMaker.MakeNewLord(null, new LordJob_ViviBaitThief(lure), map, new List<Pawn> { pawn });

            Messages.Message(LocalizeString_Message.VV_Message_ViviLureBear.Translate(pawn.Named("PAWN")).CapitalizeFirst(), new LookTargets(pawn, lure), MessageTypeDefOf.NeutralEvent);
            return true;
        }

        private static bool TryStartHornet(Building_ViviLure lure)
        {
            var map = lure.Map;
            if (!BeekeepingUtility.TryFindEntryCellReaching(map, lure.Position, out var entryCell)) { return false; }

            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(VVPawnKindDefOf.VV_TitanicHornet, null, PawnGenerationContext.NonPlayer, tile: map.Tile));
            GenSpawn.Spawn(pawn, entryCell, map);

            pawn.mindState.mentalStateHandler.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk);
            pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + HornetStayTicks;

            Find.LetterStack.ReceiveLetter(
                LocalizeString_Letter.VV_Letter_ViviLureHornetLabel.Translate(),
                LocalizeString_Letter.VV_Letter_ViviLureHornet.Translate(pawn.Named("PAWN")),
                LetterDefOf.ThreatSmall,
                new LookTargets(pawn, lure));
            return true;
        }
    }
}
