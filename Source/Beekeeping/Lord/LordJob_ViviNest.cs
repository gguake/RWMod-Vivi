using RimWorld;
using System.Collections.Generic;
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

        // 남은 식량이 이만큼(일)보다 적고 모을 꽃도 없는 채로 이만큼(일) 지나면 굶어 죽기 전에 떠난다.
        // 예전 조건(식량 0 + 평균 배고픔 0.1 미만 2일)은 평균이 그만큼 떨어지기 전에 하나씩 굶어 죽어 둥지가 전멸했다(장기 계측: 북방림 겨울).
        // 꽃은 채집할 만큼 자란 것만 센다. 겨울에 갓 심어 자라지 못하는 꽃까지 셌더니 식량이 바닥난 채 떠나지 않아 아기가 굶어 죽었다(장기 계측: 온대림 겨울).
        public const float LowFoodDaysToLeave = 1f;
        public const int MinFlowersToStay = 10;
        public const float StarvingDaysToLeave = 0.5f;
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

            // 플레이어가 구성원을 노려 죽이거나 붙잡으려 하면 경계심과 상관없이 바로 적대해 달아난다(빗나간 공격에 죽은 것·바닐라 호감도가 봐주는 살해는 뺀다).
            var outrage = new Transition(liveToil, fleeToil);
            outrage.AddTrigger(new Trigger_Custom(signal => signal.type == TriggerSignalType.PawnArrestAttempted
                || (signal.type == TriggerSignalType.PawnLost && signal.condition == PawnLostCondition.Killed && IsOutrageousKill(signal.thing, signal.dinfo))));
            outrage.AddPreAction(new TransitionAction_Custom(() => _hostility.TurnHostile()));
            outrage.AddPostAction(new TransitionAction_WakeAll());
            outrage.AddPostAction(new TransitionAction_EndAllJobs());
            graph.transitions.Insert(0, outrage);

            var trapped = new Transition(leaveToil, fleeToil);
            trapped.AddTrigger(new Trigger_PawnCannotReachMapEdge());
            graph.AddTransition(trapped);

            return graph;
        }

        // 방에 들어오면 영역을 잡고 방 안의 플레이어 구역을 지운다. 방 문은 금지하지 않는다(정착민이 들어가지 않게 막는 것은 플레이어 몫, 경계심 참고).
        private void Settle()
        {
            if (_settled) { return; }
            _settled = true;

            _territory.Establish(_nestCell);
            _planner.Notify_Established();

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

        // 맵의 건물이 서거나 없어졌다(바닐라 맵 사건을 둥지 토일이 넘긴다). 미리 만들어 둔 칸 집합(경계 문·작업 자리)과 증축 자리 실패 기억을 다시 본다.
        public void Notify_BuildingChanged(Building building)
        {
            if (!_settled || building == null) { return; }

            _territory.Notify_BuildingChanged(building);
            _production.Notify_BuildingChanged(building);
            _planner.Notify_BuildingChanged(building);
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

            var starving = fed > 0 && ((_production.FoodReserveDays() < LowFoodDaysToLeave && _garden.GatherableFlowerCount < MinFlowersToStay)
                || (_stock.FoodNutrition <= 0.01f && foodSum / fed < 0.1f));
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

        // 플레이어 탓으로 치는 피해. 바닐라 호감도 판정(Faction.Notify_MemberTookDamage)처럼 날뛰는(공격적 정신 이상) 구성원을 친 것,
        // 날뛰는 폰(인간 사냥 중인 정착지 동물 등)·반란 중인 노예가 가한 것, 둘 다의 적을 노리다 빗맞은 것은 치지 않는다.
        public static bool IsPlayerHarm(Pawn victim, DamageInfo dinfo)
        {
            var instigator = dinfo.Instigator;
            if (instigator == null || instigator.Faction != Faction.OfPlayer) { return false; }
            if (victim.InAggroMentalState) { return false; }
            if (instigator is Pawn pawn && (pawn.InAggroMentalState || SlaveRebellionUtility.IsRebelling(pawn))) { return false; }

            return !IsMutuallyHostileCrossfire(dinfo, victim.Faction);
        }

        // 바닐라 Faction.IsMutuallyHostileCrossfire와 같다(비공개라 옮겨 둔다).
        private static bool IsMutuallyHostileCrossfire(DamageInfo dinfo, Faction victimFaction)
        {
            var target = dinfo.IntendedTarget;
            if (dinfo.Instigator == null || target == null) { return false; }
            if (target.HostileTo(dinfo.Instigator) && target.HostileTo(victimFaction)) { return true; }

            return target.Faction.HostileTo(dinfo.Instigator.Faction) && target.Faction.HostileTo(victimFaction);
        }

        // 그 구성원을 노린 공격(빗맞은 것이 아님). 폭발처럼 노린 대상이 없는 공격은 노린 것으로 친다.
        public static bool IsIntended(DamageInfo dinfo, Thing victim)
        {
            return dinfo.IntendedTarget == null || dinfo.IntendedTarget == victim;
        }

        // 바로 적대할 살해. 바닐라처럼 정착민을 먼저 해쳐 죄가 있던 구성원은 뺀다(죽은 뒤에는 정신 상태가 풀려 날뛰던 것은 죄로만 남는다).
        private static bool IsOutrageousKill(Thing victim, DamageInfo dinfo)
        {
            return victim is Pawn pawn && !(pawn.guilt?.IsGuilty ?? false) && IsPlayerHarm(pawn, dinfo) && IsIntended(dinfo, pawn);
        }

        // 둥지가 끝나면(떠남·전멸) 남은 둥지 청사진·골조를 치우고 둥지 건물은 무소속으로 돌린다.
        // 하나를 치울 때 같은 칸의 다른 골조가 함께 사라질 수 있어 이미 사라진 것은 건너뛴다.
        // 바닐라도 임시 세력을 지울 때 건물을 무소속으로 돌리지만(ListerBuildings.Notify_FactionRemoved), 돌리는 동안 목록이 바뀌어
        // 하나씩 건너뛰므로 지워진 세력을 든 문이 남아 길찾기마다 관계 오류가 난다.
        public override void Cleanup()
        {
            base.Cleanup();
            if (Map == null) { return; }

            foreach (var thing in _planner.NestConstructions().ToList())
            {
                if (!thing.Destroyed)
                {
                    thing.Destroy(DestroyMode.Cancel);
                }
            }

            if (_faction == null) { return; }
            foreach (var building in Map.listerBuildings.allBuildingsNonColonist.ToList())
            {
                if (building.Faction == _faction)
                {
                    building.SetFaction(null);
                }
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
            // 시체는 둥지 몫이 아니다(둥지 밖에 버린 시체를 운반이 다시 들여오지 않게). 옮기다 내려놓은 폰(쓰러진 구성원·아기)도 물건이 아니다.
            if (thing == null || thing.Destroyed || thing is Corpse || thing is Pawn) { return; }

            thing.SetForbidden(true, warnOnFail: false);
            // 놓기 직전의 산출물도 잡아 두면 둥지 밖에 놓여도 둥지 몫으로 남는다(둥지 칸에 놓이면 장부가 알아서 뺀다).
            if (!thing.Spawned || !_territory.IsTerritoryCell(thing.Position))
            {
                _stock.Claim(thing);
            }
            _stock.MarkDirty();
        }

        // 구성원이 내려놓은 물건(운반이 끊긴 것, 나눠 든 조각, 짐 풀기)을 둥지 몫으로 남긴다. 내려놓은 물건이 그 칸에 먼저 있던
        // 둥지 몫이 아닌 같은 더미(플레이어 더미)에 합쳐졌으면 그 더미는 잡지 않는다(플레이어 물건을 가져가지 않는다). 막 생긴 더미는 이번 틱에 놓인 것이다.
        public void MarkDropped(Thing thing)
        {
            if (thing == null) { return; }
            if (thing.Spawned && thing.TickSpawned != Find.TickManager.TicksGame && !_stock.IsNestOwned(thing)) { return; }

            MarkOwned(thing);
        }

        // 둥지 몫으로 표시해 center 둘레 바닥에 놓는다. 바닐라 놓기는 그 칸의 같은 더미에 합치고 합친 더미는 그 더미의 금지 상태를 따르므로
        // 플레이어 더미에 합쳐지면 둥지 몫이 사라진다. 그래서 둥지 몫이 아닌 같은 물건이 놓인 칸은 피한다.
        public bool TryPlaceOwned(Thing thing, IntVec3 center)
        {
            var map = Map;
            var def = thing.def;
            MarkOwned(thing);
            if (!GenPlace.TryPlaceThing(thing, center, map, ThingPlaceMode.Near, out var placed, extraValidator: c => !HasForeignStack(map, c, def)))
            {
                return false;
            }

            MarkOwned(placed);
            return true;
        }

        private bool HasForeignStack(Map map, IntVec3 cell, ThingDef def)
        {
            var things = map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; ++i)
            {
                if (things[i].def == def && !_stock.IsNestOwned(things[i])) { return true; }
            }
            return false;
        }

        // 둥지 비비의 침대: 자기 세력 침대(바닐라 FindBedFor). 둥지 부화기는 알과 아기(아기 침대)만 쓰고 어린이는 둥지 방 바닥에서 잔다.
        // 비비 어린이(몸 크기 0.28)는 부화기(아기 침대, 0.3 이하)에 들어가 계측(probe_growth)에서 낮잠·밤잠마다 부화기를 다시 차지했다
        // (바닐라는 생일마다 소유를 풀 뿐이다). 어린이 시기(12살까지, 약 45일) 내내 하나뿐인 부화기를 쥐어 로열이 알을 못 낳았다
        // (장기 계측: 온대 60일 내내 6명, 로열 알 게이지가 40일 동안 가득 찬 채).
        public Building_Bed FindBedFor(Pawn pawn)
        {
            var bed = RestUtility.FindBedFor(pawn);
            return bed is ViviEggHatchery && !pawn.DevelopmentalStage.Baby() ? null : bed;
        }

        // 잠자리는 편한 기온의 둥지 방에서 고른다. 증축 방에는 잔불꽃이 없어 겨울에 영하로 내려가는데 방을 가리지 않고 골랐더니,
        // 그 방에서 자던 아이가 동상으로 죽었다(장기 계측: 온대림 겨울, 증축 방 -12°C).
        public bool TryFindSleepSpot(Pawn pawn, out IntVec3 cell) => TryFindRoomCell(pawn, ComfortableRooms(pawn), out cell);

        // 고른 둥지 방들 중 설 수 있고 건물이 없는 칸
        public bool TryFindRoomCell(Pawn pawn, IReadOnlyList<Room> rooms, out IntVec3 cell)
        {
            var map = Map;
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

        private static readonly List<Room> _tmpComfortableRooms = new List<Room>();

        // 그 비비에게 편한 기온의 둥지 방들. 그런 방이 없으면 기온이 편한 범위에서 가장 덜 벗어난 방 하나.
        public List<Room> ComfortableRooms(Pawn pawn)
        {
            _tmpComfortableRooms.Clear();
            var range = pawn.ComfortableTemperatureRange();
            Room best = null;
            var bestGap = float.MaxValue;
            var rooms = _territory.Rooms;
            for (int i = 0; i < rooms.Count; ++i)
            {
                var temperature = rooms[i].Temperature;
                var gap = temperature < range.min ? range.min - temperature : (temperature > range.max ? temperature - range.max : 0f);
                if (gap <= 0f)
                {
                    _tmpComfortableRooms.Add(rooms[i]);
                }
                else if (gap < bestGap)
                {
                    best = rooms[i];
                    bestGap = gap;
                }
            }

            if (_tmpComfortableRooms.Count == 0 && best != null) { _tmpComfortableRooms.Add(best); }
            return _tmpComfortableRooms;
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
