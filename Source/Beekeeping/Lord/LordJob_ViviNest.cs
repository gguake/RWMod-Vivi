using RimWorld;
using System.Linq;
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
        private ViviNestProduction _production;
        private ViviNestPlanner _planner;
        private ViviNestGarden _garden;
        private ViviNestHostility _hostility;

        private int _starvingSince = -1;
        private int _roomLostSince = -1;

        public Faction Faction => _faction;
        public IntVec3 NestCell => _nestCell;
        public bool Settled => _settled;
        public ViviNestTerritory Territory => _territory;
        public ViviNestStock Stock => _stock;
        public ViviNestProduction Production => _production;
        public ViviNestPlanner Planner => _planner;
        public ViviNestGarden Garden => _garden;
        public ViviNestHostility Hostility => _hostility;

        public const float StarvingDaysToLeave = 2f;
        public const float RoomLostDaysToLeave = 1f;

        public override bool AddFleeToil => false;

        // 습격 등으로 모두 쓰러져도 둥지가 흩어지지 않게 쓰러진 비비도 남긴다(바닐라 벌레 둥지와 같은 방식).
        public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason)
        {
            return reason != PawnLostCondition.Incapped;
        }

        public LordJob_ViviNest()
        {
            _territory = new ViviNestTerritory(this);
            _stock = new ViviNestStock(this);
            _production = new ViviNestProduction(this);
            _planner = new ViviNestPlanner(this);
            _garden = new ViviNestGarden(this);
            _hostility = new ViviNestHostility(this);
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

            // 떠날 때는 어른이 아기를 안고 나가도록 둥지 나가기 토일을 쓴다(평화 이주는 걷고, 적대 도주는 뛰며 막히면 판다).
            var leaveToil = new LordToil_ViviNestExit(LocomotionUrgency.Walk, canDig: false);
            graph.AddToil(leaveToil);

            var fleeToil = new LordToil_ViviNestExit(LocomotionUrgency.Jog, canDig: true);
            graph.AddToil(fleeToil);

            var arrived = new Transition(travelToil, liveToil);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            arrived.AddPreAction(new TransitionAction_Custom(Settle));
            graph.AddTransition(arrived);

            var abandon = new Transition(liveToil, leaveToil);
            BeekeepingUtility.AddSourcesDistinct(abandon, travelSubgraph.lordToils);
            abandon.AddTrigger(new Trigger_Memo(MemoAbandon));
            abandon.AddPostAction(new TransitionAction_WakeAll());
            abandon.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(abandon);

            // 적대가 되면 무엇을 하던 바로 달아난다. 하위 그래프의 전이보다 먼저 검사되게 맨 앞에 둔다.
            var flee = new Transition(liveToil, fleeToil);
            BeekeepingUtility.AddSourcesDistinct(flee, travelSubgraph.lordToils);
            flee.AddSource(leaveToil);
            flee.AddTrigger(new Trigger_BecamePlayerEnemy());
            flee.AddPostAction(new TransitionAction_WakeAll());
            flee.AddPostAction(new TransitionAction_EndAllJobs());
            graph.transitions.Insert(0, flee);

            // 플레이어가 구성원을 죽이거나 붙잡으려 하면 경계심과 상관없이 바로 적대해 달아난다.
            var outrage = new Transition(liveToil, fleeToil);
            outrage.AddTrigger(new Trigger_Custom(signal => signal.type == TriggerSignalType.PawnArrestAttempted
                || (signal.type == TriggerSignalType.PawnLost && signal.condition == PawnLostCondition.Killed && signal.dinfo.Instigator?.Faction == Faction.OfPlayer)));
            outrage.AddPreAction(new TransitionAction_Custom(() => _hostility.TurnHostile()));
            outrage.AddPostAction(new TransitionAction_WakeAll());
            outrage.AddPostAction(new TransitionAction_EndAllJobs());
            graph.transitions.Insert(0, outrage);

            var trapped = new Transition(leaveToil, fleeToil);
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
            _planner.Notify_Established();

            // 정착할 때 방 안에 있던 정착민은 나갈 때까지 경계하지 않는다. 문은 방이 비면 설계자가 금지한다(안에 가두지 않게).
            _hostility.Notify_Settled();
            _planner.ForbidNestDoors();

            var zoneManager = Map.zoneManager;
            foreach (var room in _territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    zoneManager.ZoneAt(cell)?.RemoveCell(cell);
                }
            }

            foreach (var pawn in lord.ownedPawns)
            {
                JobGiver_ViviNestBuild.EnsureBuilderWorkSettings(pawn);
            }

            _stock.MarkDirty();
        }

        public override void Notify_PawnAdded(Pawn p)
        {
            base.Notify_PawnAdded(p);
            if (_settled)
            {
                JobGiver_ViviNestBuild.EnsureBuilderWorkSettings(p);
            }
        }

        public override void LordJobTick()
        {
            base.LordJobTick();
            if (!_settled) { return; }

            var ticks = Find.TickManager.TicksGame;
            if (ticks % ViviNestProduction.RefreshInterval == 0)
            {
                _production.Tick();
            }
            // 생산 갱신과 같은 틱에 몰리지 않게 반 주기 어긋나게 계획한다.
            if ((ticks + ViviNestPlanner.PlanInterval / 2) % ViviNestPlanner.PlanInterval == 0)
            {
                _planner.Tick();
                CheckDeparture();
            }
            if ((ticks + ViviNestHostility.CheckInterval / 2) % ViviNestHostility.CheckInterval == 0 && lord.CurLordToil is LordToil_ViviNestLive)
            {
                _hostility.Tick();
            }
        }

        // 평화 이주: 굶주림이 이틀, 둥지 방을 잃은 채 하루가 이어지거나, 어른이 한 명 이하로 남으면 걸어 나간다.
        private void CheckDeparture()
        {
            if (!(lord.CurLordToil is LordToil_ViviNestLive)) { return; }

            var ticks = Find.TickManager.TicksGame;
            var pawns = lord.ownedPawns;

            var adults = 0;
            var foodSum = 0f;
            var fed = 0;
            for (int i = 0; i < pawns.Count; ++i)
            {
                if (pawns[i].DevelopmentalStage.Adult()) { adults++; }
                if (pawns[i].needs?.food != null)
                {
                    foodSum += pawns[i].needs.food.CurLevelPercentage;
                    fed++;
                }
            }

            var starving = _stock.FoodNutrition <= 0.01f && fed > 0 && foodSum / fed < 0.1f;
            _starvingSince = starving ? (_starvingSince < 0 ? ticks : _starvingSince) : -1;

            var roomCells = _territory.OwnedRoomCells.Count;
            var unroofed = 0;
            foreach (var _ in _planner.CellsNeedingRoof()) { unroofed++; }
            var roomLost = !_territory.HasAnyRoom || (roomCells > 0 && unroofed * 2 >= roomCells);
            _roomLostSince = roomLost ? (_roomLostSince < 0 ? ticks : _roomLostSince) : -1;

            string reason = null;
            if (adults <= 1)
            {
                reason = LocalizeString_Message.VV_Message_ViviNestLeavingFew;
            }
            else if (_starvingSince >= 0 && ticks - _starvingSince >= StarvingDaysToLeave * GenDate.TicksPerDay)
            {
                reason = LocalizeString_Message.VV_Message_ViviNestLeavingStarving;
            }
            else if (_roomLostSince >= 0 && ticks - _roomLostSince >= RoomLostDaysToLeave * GenDate.TicksPerDay)
            {
                reason = LocalizeString_Message.VV_Message_ViviNestLeavingRoomLost;
            }

            if (reason != null)
            {
                Messages.Message(reason.Translate(_faction.Name), new LookTargets(pawns), MessageTypeDefOf.NeutralEvent);
                lord.ReceiveMemo(MemoAbandon);
            }
        }

        public override string GetReport(Pawn pawn)
        {
            return _settled ? LocalizeString_Inspector.VV_Inspector_ViviNestWariness.Translate((_hostility.Level / ViviNestHostility.Max).ToStringPercent()).ToString() : null;
        }

        // 구성원을 가두면 바로 적대한다.
        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            if (_settled && condition == PawnLostCondition.MadePrisoner)
            {
                _hostility.TurnHostile();
            }
        }

        // 둥지가 끝나면(떠남·전멸) 남은 둥지 청사진·골조를 치운다. 건물은 임시 세력이 사라질 때 바닐라가 무소속으로 돌린다.
        public override void Cleanup()
        {
            base.Cleanup();
            if (Map == null) { return; }

            foreach (var thing in _planner.NestConstructions().ToList())
            {
                thing.Destroy(DestroyMode.Cancel);
            }
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
            // 놓기 직전의 산출물도 잡아 두면 둥지 밖에 놓여도 둥지 몫으로 남는다(둥지 칸에 놓이면 장부가 알아서 뺀다).
            if (!thing.Spawned || !_territory.IsTerritoryCell(thing.Position))
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
            Scribe_Deep.Look(ref _production, "production", this);
            Scribe_Deep.Look(ref _planner, "planner", this);
            Scribe_Deep.Look(ref _garden, "garden", this);
            Scribe_Deep.Look(ref _hostility, "hostility", this);
            Scribe_Values.Look(ref _starvingSince, "starvingSince", -1);
            Scribe_Values.Look(ref _roomLostSince, "roomLostSince", -1);
        }
    }
}
