using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 꿀 냄새를 맡은 곰·거대 말벌이 유인통으로 와서 미끼를 먹어 치우고 떠난다.
    public class LordJob_ViviBaitThief : LordJob
    {
        public const string MemoBaitDevoured = "VV_BaitDevoured";
        private const int MaxStayTicks = 6000;
        private const int MaxTravelTicks = GenDate.TicksPerDay / 2;

        private Building_ViviLure _lure;
        // 만들 때의 유인통 자리. 그래프(이동 목적지·미끼 먹기 위치)는 이것만 쓴다(불러올 때 다시 만드는 그래프가 처음과 같도록, LordJob_ViviLureScout와 같다).
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

            // 미끼를 다 먹었거나 오래 머물면 떠난다. 머무는 시간은 도착한 뒤부터 센다(느린 동물은 오는 데만 오래 걸린다).
            var done = new Transition(devourToil, exitToil);
            done.AddTrigger(new Trigger_Memo(MemoBaitDevoured));
            done.AddTrigger(new Trigger_TicksPassed(MaxStayTicks));
            done.AddPostAction(new TransitionAction_Custom(EndVisit));
            graph.AddTransition(done);

            // 다치거나 미끼가 사라지면 오는 길에도 떠난다. 이동 하위 그래프의 '피격 → 지점 방어'보다 먼저 검사되게 맨 앞에 둔다.
            var abort = new Transition(devourToil, exitToil);
            BeekeepingUtility.AddSourcesDistinct(abort, travelSubgraph.lordToils);
            abort.AddTrigger(new Trigger_PawnHarmed());
            abort.AddTrigger(new Trigger_TickCondition(() => _lure == null || !_lure.Spawned || !_lure.HasBait, 250));
            abort.AddTrigger(new Trigger_TicksPassed(MaxTravelTicks));
            abort.AddPostAction(new TransitionAction_Custom(EndVisit));
            graph.transitions.Insert(0, abort);

            return graph;
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
