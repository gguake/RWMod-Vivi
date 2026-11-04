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

        private const int SleepSpotTries = 30;

        private Faction _faction;
        private IntVec3 _nestCell;
        private bool _settled;

        private ViviNestTerritory _territory;
        private ViviNestStock _stock;

        public Faction Faction => _faction;
        public IntVec3 NestCell => _nestCell;
        public bool Settled => _settled;
        public ViviNestTerritory Territory => _territory;
        public ViviNestStock Stock => _stock;

        public override bool AddFleeToil => false;

        public LordJob_ViviNest()
        {
            _territory = new ViviNestTerritory(this);
            _stock = new ViviNestStock(this);
        }

        public LordJob_ViviNest(Faction faction, IntVec3 nestCell) : this()
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
            arrived.AddPreAction(new TransitionAction_Custom(Settle));
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

        // 방에 들어오면 영역을 잡고, 정착민이 드나들지 않게 플레이어 문을 금지하고, 방 안의 플레이어 구역을 지운다.
        private void Settle()
        {
            if (_settled) { return; }
            _settled = true;

            _territory.Establish(_nestCell);

            foreach (var door in _territory.BorderDoors)
            {
                if (door.Faction == Faction.OfPlayer)
                {
                    door.SetForbidden(true, warnOnFail: false);
                }
            }

            var zoneManager = Map.zoneManager;
            foreach (var room in _territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    zoneManager.ZoneAt(cell)?.RemoveCell(cell);
                }
            }

            _stock.MarkDirty();
        }

        // 둥지 비비가 쓸 수 있는 것: 둥지 물건, 둥지 세력·무소속 건물, 그 밖의 식물·폰 등. 플레이어 건물·물건은 쓰지 않는다.
        public bool CanUse(Thing thing)
        {
            if (thing is Pawn) { return true; }
            if (thing.def.category == ThingCategory.Item) { return _stock.IsNestOwned(thing); }
            if (thing is Building building) { return building.Faction != Faction.OfPlayer || building is Building_Door; }
            return true;
        }

        public void MarkOwned(Thing thing)
        {
            if (thing == null || thing.Destroyed) { return; }

            thing.SetForbidden(true, warnOnFail: false);
            if (thing.Spawned && !_territory.IsTerritoryCell(thing.Position))
            {
                _stock.Claim(thing);
            }
            _stock.MarkDirty();
        }

        public bool TryFindSleepSpot(Pawn pawn, out IntVec3 cell)
        {
            var map = Map;
            var rooms = _territory.Rooms;
            for (int i = 0; i < SleepSpotTries && rooms.Count > 0; ++i)
            {
                if (!rooms.RandomElement().Cells.TryRandomElement(out cell)) { continue; }
                if (!cell.Standable(map) || cell.GetTerrain(map).avoidWander || cell.GetEdifice(map) != null) { continue; }
                if (!pawn.CanReserve(cell) || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some)) { continue; }
                return true;
            }

            cell = IntVec3.Invalid;
            return false;
        }

        public bool TryGetRandomRoomCell(out IntVec3 cell)
        {
            var rooms = _territory.Rooms;
            if (rooms.Count > 0 && rooms.RandomElement().Cells.TryRandomElement(out cell))
            {
                return true;
            }

            cell = IntVec3.Invalid;
            return false;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref _faction, "faction");
            Scribe_Values.Look(ref _nestCell, "nestCell");
            Scribe_Values.Look(ref _settled, "settled");
            Scribe_Deep.Look(ref _territory, "territory", this);
            Scribe_Deep.Look(ref _stock, "stock", this);
        }
    }
}
