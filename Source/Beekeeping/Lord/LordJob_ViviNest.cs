using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 정착한 떠돌이 비비 무리. 정착 이후의 둥지 상태는 모두 이 로드잡이 저장한다.
    public class LordJob_ViviNest : LordJob
    {
        public const string MemoAbandon = "VV_NestAbandon";

        private Faction _faction;
        private IntVec3 _nestCell;

        public Faction Faction => _faction;
        public IntVec3 NestCell => _nestCell;

        public override bool AddFleeToil => false;

        public LordJob_ViviNest()
        {
        }

        public LordJob_ViviNest(Faction faction, IntVec3 nestCell)
        {
            _faction = faction;
            _nestCell = nestCell;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();

            var travelSubgraph = new LordJob_Travel(_nestCell).CreateGraph();
            var travelToil = graph.AttachSubgraph(travelSubgraph).StartingToil;
            graph.StartingToil = travelToil;

            var liveToil = new LordToil_ViviNestLive(_nestCell);
            graph.AddToil(liveToil);

            var leaveSubgraph = new LordJob_TravelAndExit(IntVec3.Invalid).CreateGraph();
            var leaveToil = graph.AttachSubgraph(leaveSubgraph).StartingToil;

            var fleeToil = new LordToil_ExitMap(LocomotionUrgency.Jog, canDig: true, interruptCurrentJob: true);
            graph.AddToil(fleeToil);

            var arrived = new Transition(travelToil, liveToil);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            graph.AddTransition(arrived);

            var abandon = new Transition(liveToil, leaveToil);
            BeekeepingUtility.AddSourcesDistinct(abandon, travelSubgraph.lordToils);
            abandon.AddTrigger(new Trigger_Memo(MemoAbandon));
            abandon.AddPreAction(new TransitionAction_EnsureHaveExitDestination());
            abandon.AddPostAction(new TransitionAction_WakeAll());
            abandon.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(abandon);

            // 적대가 되면 무엇을 하던 바로 달아난다. 하위 그래프의 전이보다 먼저 검사되게 맨 앞에 둔다.
            var flee = new Transition(liveToil, fleeToil);
            BeekeepingUtility.AddSourcesDistinct(flee, travelSubgraph.lordToils);
            BeekeepingUtility.AddSourcesDistinct(flee, leaveSubgraph.lordToils);
            flee.AddTrigger(new Trigger_BecamePlayerEnemy());
            flee.AddPostAction(new TransitionAction_WakeAll());
            graph.transitions.Insert(0, flee);

            var trapped = new Transition(leaveToil, fleeToil);
            BeekeepingUtility.AddSourcesDistinct(trapped, leaveSubgraph.lordToils);
            trapped.AddTrigger(new Trigger_PawnCannotReachMapEdge());
            graph.AddTransition(trapped);

            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref _faction, "faction");
            Scribe_Values.Look(ref _nestCell, "nestCell");
        }
    }
}
