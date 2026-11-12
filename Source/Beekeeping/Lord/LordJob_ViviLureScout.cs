using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordJob_ViviLureScout : LordJob
    {
        private Faction _faction;
        private Building_ViviLure _lure;
        
        private IntVec3 _lurePos = IntVec3.Invalid;
        private int _inspectTicks;
        private bool _resultDecided;

        private List<Pawn> _sniffedPawns = new List<Pawn>();
        private List<Pawn> _roomVisitedPawns = new List<Pawn>();

        public Building_ViviLure Lure => _lure;

        public override bool AddFleeToil => false;

        public LordJob_ViviLureScout()
        {
        }

        public LordJob_ViviLureScout(Faction faction, Building_ViviLure lure, int inspectTicks)
        {
            _faction = faction;
            _lure = lure;
            _lurePos = lure?.Position ?? IntVec3.Invalid;
            _inspectTicks = inspectTicks;
        }

        public bool HasSniffed(Pawn pawn) => _sniffedPawns.Contains(pawn);
        public void Notify_Sniffed(Pawn pawn)
        {
            if (!_sniffedPawns.Contains(pawn)) { _sniffedPawns.Add(pawn); }
        }

        public bool HasVisitedRoom(Pawn pawn) => _roomVisitedPawns.Contains(pawn);
        public void Notify_RoomVisited(Pawn pawn)
        {
            if (!_roomVisitedPawns.Contains(pawn)) { _roomVisitedPawns.Add(pawn); }
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();

            var travelSubgraph = new LordJob_Travel(_lurePos).CreateGraph();
            var travelToil = graph.AttachSubgraph(travelSubgraph).StartingToil;
            graph.StartingToil = travelToil;

            var inspectToil = new LordToil_ViviDuty(VVDutyDefOf.VV_ViviLureScout, _lurePos);
            graph.AddToil(inspectToil);

            var exitSubgraph = new LordJob_TravelAndExit(IntVec3.Invalid).CreateGraph();
            var exitToil = graph.AttachSubgraph(exitSubgraph).StartingToil;

            var exitMapToil = new LordToil_ExitMap(LocomotionUrgency.Walk, canDig: true);
            graph.AddToil(exitMapToil);

            var arrived = new Transition(travelToil, inspectToil);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            graph.AddTransition(arrived);

            var done = new Transition(inspectToil, exitToil);
            done.AddTrigger(new Trigger_TicksPassed(_inspectTicks));
            done.AddPreAction(new TransitionAction_Custom(() => FinishScouting(completed: true)));
            done.AddPreAction(new TransitionAction_EnsureHaveExitDestination());
            done.AddPostAction(new TransitionAction_WakeAll());
            graph.AddTransition(done);

            var danger = new Transition(travelToil, exitToil);
            BeekeepingUtility.AddSourcesDistinct(danger, travelSubgraph.lordToils);
            danger.AddSource(inspectToil);
            danger.AddTrigger(new Trigger_PawnHarmed());
            danger.AddTrigger(new Trigger_PawnExperiencingDangerousTemperatures());
            danger.AddTrigger(new Trigger_PawnExperiencingAnomalousWeather());
            danger.AddTrigger(new Trigger_BecamePlayerEnemy());
            danger.AddTrigger(new Trigger_TickCondition(() => GenHostility.AnyHostileActiveThreatTo(Map, _faction), 250));
            danger.AddPreAction(new TransitionAction_Custom(() => FinishScouting(completed: false)));
            danger.AddPreAction(new TransitionAction_EnsureHaveExitDestination());
            danger.AddPostAction(new TransitionAction_EndAllJobs());
            graph.transitions.Insert(0, danger);

            var trapped = new Transition(travelToil, exitMapToil);
            BeekeepingUtility.AddSourcesDistinct(trapped, travelSubgraph.lordToils);
            trapped.AddSource(inspectToil);
            BeekeepingUtility.AddSourcesDistinct(trapped, exitSubgraph.lordToils);
            trapped.AddTrigger(new Trigger_PawnCannotReachMapEdge());
            graph.AddTransition(trapped);

            var freed = new Transition(exitMapToil, exitToil);
            freed.AddTrigger(new Trigger_PawnCanReachMapEdge());
            freed.AddPreAction(new TransitionAction_EnsureHaveExitDestination());
            freed.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(freed);

            return graph;
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);

            if (!_resultDecided && lord.ownedPawns.Count == 0 && condition != PawnLostCondition.ExitedMap)
            {
                FinishScouting(completed: false);
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();

            if (!_resultDecided)
            {
                FinishScouting(completed: false);
            }
        }

        private void FinishScouting(bool completed)
        {
            if (_resultDecided) { return; }
            _resultDecided = true;

            var lure = _lure;
            if (lure == null || !lure.Spawned || lure.ReservedFaction != _faction) { return; }

            if (!completed)
            {
                lure.ResetAttraction(releaseFaction: true);
                Messages.Message(LocalizeString_Message.VV_Message_ViviLureScoutsFled.Translate(), lure, MessageTypeDefOf.NegativeEvent);
                return;
            }

            var report = lure.EvaluateSiteNow();
            if (!report.Acceptable)
            {
                lure.ResetAttraction(releaseFaction: true);
                Messages.Message(LocalizeString_Message.VV_Message_ViviLureScoutsDisappointed.Translate(report.MissingReasons), lure, MessageTypeDefOf.NeutralEvent);
                return;
            }

            lure.Notify_SettlersScheduled(Find.TickManager.TicksGame + report.SettleDelayTicks);

            Messages.Message(LocalizeString_Message.VV_Message_ViviLureScoutsSatisfied.Translate(), lure, MessageTypeDefOf.PositiveEvent);
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && _lure != null && _lure.Destroyed)
            {
                _lure = null;
            }

            Scribe_References.Look(ref _faction, "faction");
            Scribe_References.Look(ref _lure, "lure");
            Scribe_Values.Look(ref _lurePos, "lurePos", IntVec3.Invalid);
            Scribe_Values.Look(ref _inspectTicks, "inspectTicks");
            Scribe_Values.Look(ref _resultDecided, "resultDecided");
            Scribe_Collections.Look(ref _sniffedPawns, "sniffedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref _roomVisitedPawns, "roomVisitedPawns", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _sniffedPawns.RemoveAll(p => p == null);
                _roomVisitedPawns.RemoveAll(p => p == null);
            }
        }
    }
}
