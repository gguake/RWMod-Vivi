using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordJob_ViviNest : LordJob
    {
        public const string MemoAbandon = "VV_NestAbandon";

        private const int SleepSpotTries = 30;
        private const int TravelArrivedCheckInterval = 205;
        private const float TravelArrivedRadius = 10f;

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

        public const float LowFoodDaysToLeave = 1f;
        public const int MinFlowersToStay = 10;
        public const float StarvingDaysToLeave = 0.5f;
        public const float RoomLostDaysToLeave = 1f;

        public override bool AddFleeToil => false;

        public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason)
        {
            return reason != PawnLostCondition.Incapped || (lord.CurLordToil is LordToil_ViviNestExit && !p.DevelopmentalStage.Baby());
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

            var leaveToil = new LordToil_ViviNestExit(LocomotionUrgency.Walk, canDig: false);
            graph.AddToil(leaveToil);

            var fleeToil = new LordToil_ViviNestExit(LocomotionUrgency.Jog, canDig: true);
            graph.AddToil(fleeToil);

            var siteLost = new Transition(travelToil, leaveToil);
            siteLost.AddTrigger(new Trigger_Custom(signal => signal.type == TriggerSignalType.Memo && signal.memo == "TravelArrived" && !NestRoomIntact()));
            siteLost.AddTrigger(new Trigger_TickCondition(NestSiteLost, TravelArrivedCheckInterval));
            siteLost.AddPreAction(new TransitionAction_Custom(() => Messages.Message(
                LocalizeString_Message.VV_Message_ViviNestSettlersTurnedBack.Translate(_faction.Name), new LookTargets(lord.ownedPawns), MessageTypeDefOf.NeutralEvent)));
            siteLost.AddPostAction(new TransitionAction_WakeAll());
            siteLost.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(siteLost);

            var arrived = new Transition(travelToil, liveToil);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            arrived.AddTrigger(new Trigger_TickCondition(StandingMembersArrived, TravelArrivedCheckInterval));
            arrived.AddPreAction(new TransitionAction_Custom(Settle));
            graph.AddTransition(arrived);

            var abandon = new Transition(liveToil, leaveToil);
            BeekeepingUtility.AddSourcesDistinct(abandon, travelSubgraph.lordToils);
            abandon.AddTrigger(new Trigger_Memo(MemoAbandon));
            abandon.AddPostAction(new TransitionAction_WakeAll());
            abandon.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(abandon);

            var flee = new Transition(liveToil, fleeToil);
            BeekeepingUtility.AddSourcesDistinct(flee, travelSubgraph.lordToils);
            flee.AddSource(leaveToil);
            flee.AddTrigger(new Trigger_BecamePlayerEnemy());
            flee.AddPostAction(new TransitionAction_WakeAll());
            flee.AddPostAction(new TransitionAction_EndAllJobs());
            graph.transitions.Insert(0, flee);

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

        private bool StandingMembersArrived()
        {
            var any = false;
            var pawns = lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                var pawn = pawns[i];
                if (pawn.Downed) { continue; }
                if (!pawn.Position.InHorDistOf(_nestCell, TravelArrivedRadius) || !pawn.CanReach(_nestCell, PathEndMode.ClosestTouch, Danger.Deadly)) { return false; }
                any = true;
            }
            return any;
        }

        private bool NestSiteLost()
        {
            var standing = false;
            var pawns = lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                var pawn = pawns[i];
                if (!pawn.Spawned || pawn.Downed) { continue; }
                standing = true;
                if (pawn.CanReach(_nestCell, PathEndMode.OnCell, Danger.Deadly))
                {
                    return StandingMembersArrived() && !NestRoomIntact();
                }
            }
            return standing;
        }

        private bool NestRoomIntact()
        {
            var room = _nestCell.GetRoom(Map);
            return room != null && !room.PsychologicallyOutdoors && !room.IsDoorway;
        }

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
            var roomLost = _territory.Rooms.Count == 0 || (roomCells > 0 && unroofed * 2 >= roomCells);
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

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            if (_settled && condition == PawnLostCondition.MadePrisoner)
            {
                _hostility.TurnHostile();
            }
        }

        public static bool IsPlayerHarm(Pawn victim, DamageInfo dinfo)
        {
            var instigator = dinfo.Instigator;
            if (instigator == null || instigator.Faction != Faction.OfPlayer) { return false; }
            if (victim.InAggroMentalState) { return false; }
            if (instigator is Pawn pawn && (pawn.InAggroMentalState || SlaveRebellionUtility.IsRebelling(pawn))) { return false; }

            return !IsMutuallyHostileCrossfire(dinfo, victim.Faction);
        }

        private static bool IsMutuallyHostileCrossfire(DamageInfo dinfo, Faction victimFaction)
        {
            var target = dinfo.IntendedTarget;
            if (dinfo.Instigator == null || target == null) { return false; }
            if (target.HostileTo(dinfo.Instigator) && target.HostileTo(victimFaction)) { return true; }

            return target.Faction.HostileTo(dinfo.Instigator.Faction) && target.Faction.HostileTo(victimFaction);
        }

        public static bool IsIntended(DamageInfo dinfo, Thing victim)
        {
            return dinfo.IntendedTarget == null || dinfo.IntendedTarget == victim;
        }

        private static bool IsOutrageousKill(Thing victim, DamageInfo dinfo)
        {
            return victim is Pawn pawn && !(pawn.guilt?.IsGuilty ?? false) && IsPlayerHarm(pawn, dinfo) && IsIntended(dinfo, pawn);
        }

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

        public bool CanUse(Thing thing)
        {
            if (thing is Pawn) { return true; }
            if (thing.def.category == ThingCategory.Item) { return _stock.IsNestOwned(thing) || IsFreeGear(thing); }
            if (thing is Building building) { return building.Faction != Faction.OfPlayer || building is Building_Door; }
            return true;
        }

        public bool IsFreeGear(Thing thing)
        {
            if (!(thing is Apparel) && (thing.def.equipmentType != EquipmentType.Primary || thing.def.IsStuff)) { return false; }
            if (!thing.Spawned || thing.Map != Map || !_territory.InWorkRange(thing.Position) || thing.IsInAnyStorage()) { return false; }

            var room = thing.GetRoom();
            return room == null || room.PsychologicallyOutdoors || _territory.IsNestRoomCell(thing.Position);
        }

        public void MarkOwned(Thing thing)
        {
            if (thing == null || thing.Destroyed || thing is Corpse || thing is Pawn) { return; }

            thing.SetForbidden(true, warnOnFail: false);
            if (!thing.Spawned || !_territory.IsTerritoryCell(thing.Position))
            {
                _stock.Claim(thing);
            }
            _stock.MarkDirty();
        }

        public void MarkDropped(Thing thing)
        {
            if (thing == null) { return; }
            if (thing.Spawned && thing.TickSpawned != Find.TickManager.TicksGame && !_stock.IsNestOwned(thing)) { return; }

            MarkOwned(thing);
        }

        public bool TryPlaceOwned(Thing thing, IntVec3 center)
        {
            var map = Map;
            var def = thing.def;
            MarkOwned(thing);
            if (!GenPlace.TryPlaceThing(thing, center, map, ThingPlaceMode.Near, out var placed, extraValidator: c => !_stock.HasForeignStack(c, def)))
            {
                return false;
            }

            MarkOwned(placed);
            return true;
        }

        public Building_Bed FindBedFor(Pawn pawn)
        {
            var bed = RestUtility.FindBedFor(pawn);
            return bed is ViviEggHatchery && !pawn.DevelopmentalStage.Baby() ? null : bed;
        }

        public bool TryFindSleepTarget(Pawn pawn, out LocalTargetInfo target)
        {
            var bed = FindBedFor(pawn);
            if (bed != null)
            {
                target = bed;
                return true;
            }

            if (TryFindRoomCell(pawn, ComfortableRooms(pawn), out var cell))
            {
                target = cell;
                return true;
            }

            target = LocalTargetInfo.Invalid;
            return false;
        }

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
