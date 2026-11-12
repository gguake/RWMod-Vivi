using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordJob_ViviBaitThief : LordJob
    {
        private const int MaxStayTicks = 6000;
        private const int MaxTravelTicks = GenDate.TicksPerDay / 2;

        private Building_ViviLure _lure;
        private IntVec3 _lurePos = IntVec3.Invalid;

        public Building_ViviLure Lure => _lure;

        public override bool AddFleeToil => false;

        public LordJob_ViviBaitThief()
        {
        }

        public LordJob_ViviBaitThief(Building_ViviLure lure)
        {
            _lure = lure;
            _lurePos = lure?.Position ?? IntVec3.Invalid;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();

            var travelSubgraph = new LordJob_Travel(_lurePos).CreateGraph();
            var travelToil = graph.AttachSubgraph(travelSubgraph).StartingToil;
            graph.StartingToil = travelToil;

            var devourToil = new LordToil_ViviDuty(VVDutyDefOf.VV_ViviBaitThief, _lurePos);
            graph.AddToil(devourToil);

            var exitToil = new LordToil_ExitMap(LocomotionUrgency.Walk, canDig: true);
            graph.AddToil(exitToil);

            var arrived = new Transition(travelToil, devourToil);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            graph.AddTransition(arrived);

            var done = new Transition(devourToil, exitToil);
            done.AddTrigger(new Trigger_TickCondition(() => !AnyPawnStillEating(), 60));
            done.AddTrigger(new Trigger_TicksPassed(MaxStayTicks));
            done.AddPostAction(new TransitionAction_Custom(EndVisit));
            graph.AddTransition(done);

            var abort = new Transition(devourToil, exitToil);
            BeekeepingUtility.AddSourcesDistinct(abort, travelSubgraph.lordToils);
            abort.AddTrigger(new Trigger_PawnHarmed());
            abort.AddTrigger(new Trigger_TickCondition(() => _lure == null || !_lure.Spawned || !_lure.HasBait, 250));
            abort.AddTrigger(new Trigger_TicksPassed(MaxTravelTicks));
            abort.AddPostAction(new TransitionAction_Custom(EndVisit));
            graph.transitions.Insert(0, abort);

            return graph;
        }

        private bool AnyPawnStillEating()
        {
            if (_lure == null || !_lure.Spawned) { return false; }

            var pawns = lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                if (JobGiver_EatLureBait.WantsBait(pawns[i]) && JobGiver_EatLureBait.TryFindBait(pawns[i], _lure, checkReservation: false, out _))
                {
                    return true;
                }
            }
            return false;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            EndVisit();
        }

        private void EndVisit()
        {
            if (_lure != null && _lure.Spawned && _lure.Phase == ViviLurePhase.ThiefVisiting)
            {
                _lure.ResetAttraction(releaseFaction: false);
            }
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && _lure != null && _lure.Destroyed)
            {
                _lure = null;
            }

            Scribe_References.Look(ref _lure, "lure");
            Scribe_Values.Look(ref _lurePos, "lurePos", IntVec3.Invalid);
        }
    }
}
