using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ViviNestPlanner : IExposable
    {
        public const int PlanInterval = 2500;

        public const int CellsPerShelf = 12;
        public const int CellsPerHatchery = 20;
        public const int MaxHatcheries = 4;
        public const int FloorTilesPerProject = 4;
        public const int WaxForFloor = 40;
        public const int IdleForHorseshoes = 2;
        public const int RoomCellsPerMember = 6;
        public const float MinFoodDaysForEgg = 5f;
        public static int MaxMembers => VVRaceMod.Settings.wildViviNestMaxMembers;

        private const float OutdoorBuildRadius = 8f;
        private const float CorpseDumpMinDistance = 14f;
        private const float CorpseDumpMaxDistance = 22f;
        private const int CorpseDumpRetryTicks = 600;
        private const float ExpansionSearchRadius = 16f;
        private const float ExpansionMinDistance = 7f;
        private const int ExpansionSearchStep = 3;
        private const int ResourceNeedInterval = 250;

        private readonly LordJob_ViviNest _owner;

        private List<IntVec3> _doorCells = new List<IntVec3>();
        private bool _initialFacilitiesPlanned;
        private List<IntVec3> _expansionInterior = new List<IntVec3>();
        private IntVec3 _expansionDoor = IntVec3.Invalid;
        private int _corpseDumpRetryTick = -99999;
        private bool _eggAllowed = true;
        private bool _noExpansionSite;

        private readonly Dictionary<ThingDef, int> _resourceNeed = new Dictionary<ThingDef, int>();
        private int _resourceNeedTick = -99999;

        private const int ConstructionCacheInterval = 60;
        private readonly List<Thing> _constructionCache = new List<Thing>();
        private int _constructionCacheTick = -99999;

        public ViviNestPlanner(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;
        private Faction Faction => _owner.Faction;
        private ViviNestTerritory Territory => _owner.Territory;

        public void Notify_Established()
        {
            _doorCells.Clear();
            foreach (var door in Territory.BorderDoors.OrderByDescending(Territory.IsOuterDoor))
            {
                _doorCells.Add(door.Position);
            }
        }

        public bool EggAllowed => _eggAllowed;

        public void Tick()
        {
            _eggAllowed = ComputeEggAllowed();
            ReleaseHatcheryOwners();
            RepairBoundary();
            PlanInstalls();
            _owner.Garden.EnsureCottonCells();

            if (AnyPendingProject()) { return; }
            if (TryFinishExpansion()) { return; }

            if (!_initialFacilitiesPlanned)
            {
                _initialFacilitiesPlanned = true;
                if (PlanInitialFacilities()) { return; }
            }

            if (TryPlanEmberPot()) { return; }
            if (TryPlanCooler()) { return; }
            if (TryPlanExpansion()) { return; }
            if (TryPlanHatchery()) { return; }
            if (TryPlanShelf()) { return; }
            if (TryPlanFloor()) { return; }
            if (TryPlanHorseshoesPin()) { return; }
        }

        #region 경계 수리
        private void RepairBoundary()
        {
            var map = Map;
            foreach (var cell in Territory.BoundaryCells)
            {
                if (!cell.InBounds(map)) { continue; }

                var edifice = cell.GetEdifice(map);
                if (edifice != null && (edifice.def.passability == Traversability.Impassable || edifice is Building_Door)) { continue; }
                if (HasNestConstructionAt(cell)) { continue; }

                PlaceBoundary(cell, _doorCells.Contains(cell));
            }
        }

        private Thing PlaceBoundary(IntVec3 cell, bool door)
        {
            return door ? TryPlace(ThingDefOf.Door, cell, Rot4.North, VVThingDefOf.VV_Viviwax) : TryPlace(VVThingDefOf.VV_ViviCreamWall, cell, Rot4.North, null);
        }
        #endregion

        #region 포장 식물 설치·잔불꽃 배치
        private void PlanInstalls()
        {
            var map = Map;
            var minis = map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing);
            for (int i = 0; i < minis.Count; ++i)
            {
                if (!(minis[i] is MinifiedThing mini)) { continue; }

                var inner = mini.InnerThing;
                if (inner == null) { continue; }
                var def = inner.def;
                var emitter = def == VVThingDefOf.VV_ManaEmitter;
                if (!emitter && !(inner is ArcanePlant)) { continue; }
                if (!_owner.Stock.IsNestOwned(mini) || InstallBlueprintUtility.ExistingBlueprintFor(mini) != null) { continue; }

                var cell = emitter ? FindOutdoorEmitterCell(inner)
                    : JobGiver_ViviNestPlantSeed.IsPotPlant(def) ? FindEmberPotCell(c => ArcanePlantUtility.CanPlaceArcanePlantToCell(map, c, def).Accepted)
                    : FindOutdoorInstallCell(def);
                if (!cell.IsValid) { continue; }

                MarkConstructionsDirty();
                GenConstruct.PlaceBlueprintForInstall(mini, cell, map, Rot4.North, Faction);
            }

            foreach (var plant in OutdoorEmbersToMove())
            {
                var cell = FindEmberPotCell(c => ArcanePlantUtility.CanPlaceArcanePlantToCell(map, c, plant.def, plant).Accepted);
                if (!cell.IsValid) { break; }

                MarkConstructionsDirty();
                GenConstruct.PlaceBlueprintForReinstall(plant, cell, map, Rot4.North, Faction);
            }

            var counts = EmberCounts();
            var spare = SpareRoomEmber(counts);
            if (spare != null)
            {
                var cell = FindEmberPotCell(c => EmberCount(counts, c.GetRoom(map)) == 0 && ArcanePlantUtility.CanPlaceArcanePlantToCell(map, c, spare.def, spare).Accepted);
                if (cell.IsValid)
                {
                    MarkConstructionsDirty();
                    GenConstruct.PlaceBlueprintForReinstall(spare, cell, map, Rot4.North, Faction);
                }
            }
        }

        private ArcanePlant SpareRoomEmber(Dictionary<Room, int> counts)
        {
            if (!AnyEmberlessRoom(counts, skipNoPotRooms: false) || OutdoorEmbersToMove().Count > 0) { return null; }

            var map = Map;
            Room source = null;
            var sourceEmbers = 1;
            foreach (var room in Territory.Rooms)
            {
                var embers = EmberCount(counts, room);
                if (embers > sourceEmbers || (embers == sourceEmbers && source != null && room.Temperature > source.Temperature))
                {
                    source = room;
                    sourceEmbers = embers;
                }
            }
            if (source == null) { return null; }

            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!IsUnmovedNestEmber(building, out var plant) || plant.Position.GetRoom(map) != source) { continue; }

                return plant;
            }
            return null;
        }

        public const int CellsPerEmberPot = 18;

        private readonly Dictionary<Room, int> _emberCounts = new Dictionary<Room, int>();
        private readonly List<ArcanePlant> _tmpOutdoorEmbers = new List<ArcanePlant>();
        private readonly List<Room> _tmpPotRooms = new List<Room>();
        private readonly HashSet<Room> _noPotRooms = new HashSet<Room>();

        public static int MaxEmberPots(Room room) => System.Math.Max(1, room.CellCount / CellsPerEmberPot);

        public IntVec3 FindEmberPotCell(System.Predicate<IntVec3> validator)
        {
            var map = Map;
            var counts = EmberCounts();
            var emberlessFirst = AnyEmberlessRoom(counts, skipNoPotRooms: true);
            var best = IntVec3.Invalid;
            var bestEmberless = false;
            var bestTemperature = float.MaxValue;
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!(building is ArcanePlantPot pot) || pot.Faction != Faction || !Territory.IsRoomCell(pot.Position)) { continue; }

                var cell = pot.Position;
                if (cell.GetFirstThing<ArcanePlant>(map) != null) { continue; }

                var room = cell.GetRoom(map);
                if (room == null) { continue; }
                var embers = EmberCount(counts, room);
                if (embers >= MaxEmberPots(room)) { continue; }

                var emberless = embers == 0;
                if (emberlessFirst && !emberless) { continue; }
                var temperature = room.Temperature;
                if (best.IsValid && ((bestEmberless && !emberless) || (bestEmberless == emberless && temperature >= bestTemperature))) { continue; }
                if (!validator(cell)) { continue; }

                best = cell;
                bestEmberless = emberless;
                bestTemperature = temperature;
            }
            return best;
        }

        private bool AnyEmberlessRoom(Dictionary<Room, int> counts, bool skipNoPotRooms)
        {
            var rooms = Territory.Rooms;
            for (int i = 0; i < rooms.Count; ++i)
            {
                if (EmberCount(counts, rooms[i]) == 0 && !(skipNoPotRooms && _noPotRooms.Contains(rooms[i]))) { return true; }
            }
            return false;
        }

        private Dictionary<Room, int> EmberCounts()
        {
            var map = Map;
            _emberCounts.Clear();
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.Faction != Faction) { continue; }
                if (building.def == VVThingDefOf.VV_EmberBloom || (building is ArcanePlant_Seedling seedling && seedling.MaturePlantDef == VVThingDefOf.VV_EmberBloom))
                {
                    AddEmber(building.Position);
                }
            }

            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (thing.Faction == Faction && thing is Blueprint_Install install && install.ThingToInstall?.def == VVThingDefOf.VV_EmberBloom)
                {
                    AddEmber(thing.Position);
                }
            }

            var pawns = _owner.lord?.ownedPawns;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; ++i)
                {
                    if (IsPlantingEmberSeed(pawns[i].CurJob)) { AddEmber(pawns[i].CurJob.targetB.Cell); }
                }
            }
            return _emberCounts;
        }

        private void AddEmber(IntVec3 cell)
        {
            var room = cell.GetRoom(Map);
            if (room != null && Territory.Rooms.Contains(room))
            {
                _emberCounts[room] = EmberCount(_emberCounts, room) + 1;
            }
        }

        private static int EmberCount(Dictionary<Room, int> counts, Room room) => counts.TryGetValue(room, out var count) ? count : 0;

        private static bool IsPlantingEmberSeed(Job job)
        {
            return job?.def == VVJobDefOf.VV_PlantArcaneSeed && JobGiver_ViviNestPlantSeed.IsPotPlant(job.targetA.Thing?.TryGetComp<CompArcaneSeed>()?.Props.targetPlantDef);
        }

        private List<ArcanePlant> OutdoorEmbersToMove()
        {
            var map = Map;
            _tmpOutdoorEmbers.Clear();
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!IsUnmovedNestEmber(building, out var plant) || Territory.IsNestRoomCell(plant.Position)) { continue; }

                _tmpOutdoorEmbers.Add(plant);
            }
            return _tmpOutdoorEmbers;
        }

        private bool IsUnmovedNestEmber(Building building, out ArcanePlant plant)
        {
            plant = null;
            if (building.def != VVThingDefOf.VV_EmberBloom || building.Faction != Faction || !(building is ArcanePlant ember)) { return false; }

            plant = ember;
            return !Map.listerBuildings.TryGetReinstallBlueprint(ember, out _);
        }

        private int EmbersToPlace()
        {
            var map = Map;
            var count = 0;
            var seedDef = VVThingDefOf.VV_EmberBloom.GetModExtension<ArcaneSeedExtension>()?.seedDef;
            if (seedDef != null)
            {
                foreach (var seed in map.listerThings.ThingsOfDef(seedDef))
                {
                    if (_owner.Stock.IsNestOwned(seed)) { count += seed.stackCount; }
                }

                var pawns = _owner.lord?.ownedPawns;
                if (pawns != null)
                {
                    for (int i = 0; i < pawns.Count; ++i)
                    {
                        if (IsPlantingEmberSeed(pawns[i].CurJob) && pawns[i].CurJob.targetA.Thing.Spawned) { count--; }
                    }
                }
            }

            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing))
            {
                if (thing is MinifiedThing mini && mini.InnerThing?.def == VVThingDefOf.VV_EmberBloom && _owner.Stock.IsNestOwned(mini)
                    && InstallBlueprintUtility.ExistingBlueprintFor(mini) == null)
                {
                    count++;
                }
            }

            return count + OutdoorEmbersToMove().Count;
        }

        private bool TryPlanEmberPot()
        {
            var counts = EmberCounts();
            var waiting = EmbersToPlace();
            var spareOnly = false;
            if (waiting <= 0)
            {
                if (SpareRoomEmber(counts) == null) { return false; }
                waiting = 1;
                spareOnly = true;
            }

            foreach (var emberless in new[] { true, false })
            {
                if (spareOnly && !emberless) { break; }

                _tmpPotRooms.Clear();
                foreach (var room in Territory.Rooms)
                {
                    var cap = MaxEmberPots(room);
                    var embers = EmberCount(counts, room);
                    if (embers >= cap || (embers == 0) != emberless) { continue; }

                    var pots = CountNestPots(room, out var freePots);
                    waiting -= System.Math.Min(freePots, cap - embers);
                    if (pots < cap && freePots == 0) { _tmpPotRooms.Add(room); }
                }
                if (waiting <= 0) { return false; }

                _tmpPotRooms.SortBy(r => r.Temperature);
                foreach (var room in _tmpPotRooms)
                {
                    foreach (var cell in WallSideRoomCells(room))
                    {
                        if (TryPlace(VVThingDefOf.VV_ArcanePlantPot, cell, Rot4.North, VVThingDefOf.VV_Viviwax) != null)
                        {
                            _noPotRooms.Remove(room);
                            return true;
                        }
                    }
                    if (emberless) { _noPotRooms.Add(room); }
                }
            }
            return false;
        }

        private int CountNestPots(Room room, out int free)
        {
            var map = Map;
            var count = 0;
            free = 0;
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!(building is ArcanePlantPot pot) || pot.Faction != Faction || pot.Position.GetRoom(map) != room) { continue; }

                count++;
                if (pot.Position.GetFirstThing<ArcanePlant>(map) == null) { free++; }
            }
            return count;
        }

        private IntVec3 FindOutdoorInstallCell(ThingDef plantDef)
        {
            var map = Map;
            var mana = map.GetManaComponent();
            var best = IntVec3.Invalid;
            var bestFertility = 0f;
            var bestMana = -1f;
            var cells = Territory.ActivityCells;
            var cellCount = Territory.ActivityCellCount(JobGiver_ViviNestPlantSeed.OutdoorSearchRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = cells[i];
                var fertility = map.fertilityGrid.FertilityAt(cell);
                var cellMana = mana != null ? mana[cell] : 0f;
                if (fertility < JobGiver_ViviNestPlantSeed.MinFertility || fertility < bestFertility || (fertility == bestFertility && cellMana <= bestMana)) { continue; }
                if (!IsFreeOutdoorCell(cell, allowPlants: true)) { continue; }

                var plant = cell.GetPlant(map);
                if (plant != null && ViviNestGarden.IsHoneyFlowerDef(plant.def)) { continue; }
                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(map, cell, plantDef).Accepted) { continue; }

                best = cell;
                bestFertility = fertility;
                bestMana = cellMana;
            }
            return best;
        }

        private const float OutdoorEmitterMinDistance = 7f;
        private const float OutdoorEmitterMaxDistance = 10f;
        private const float OutdoorEmitterSpacing = 10f;
        private const float EmitterSeedRadius = 7f;

        private readonly List<IntVec3> _tmpEmitterCells = new List<IntVec3>();

        private IntVec3 FindOutdoorEmitterCell(Thing emitter)
        {
            if (!_initialFacilitiesPlanned) { return IntVec3.Invalid; }

            var map = Map;
            _tmpEmitterCells.Clear();
            foreach (var building in map.listerThings.ThingsOfDef(VVThingDefOf.VV_ManaEmitter))
            {
                if (building.Faction == Faction) { _tmpEmitterCells.Add(building.Position); }
            }
            foreach (var thing in NestConstructions())
            {
                if (thing.def.entityDefToBuild == VVThingDefOf.VV_ManaEmitter) { _tmpEmitterCells.Add(thing.Position); }
            }

            var best = IntVec3.Invalid;
            var bestScore = 0;
            var cells = Territory.ActivityCells;
            var start = Territory.ActivityCellCount(OutdoorEmitterMinDistance);
            var end = Territory.ActivityCellCount(OutdoorEmitterMaxDistance);
            for (int i = start; i < end; ++i)
            {
                var cell = cells[i];
                if (!IsFreeOutdoorCell(cell, allowPlants: true)) { continue; }
                var plant = cell.GetPlant(map);
                if (plant != null && ViviNestGarden.IsHoneyFlowerDef(plant.def)) { continue; }
                if (_tmpEmitterCells.Any(c => c.InHorDistOf(cell, OutdoorEmitterSpacing))) { continue; }

                var score = SeedCellsAround(cell);
                if (score <= bestScore) { continue; }
                if (!GenConstruct.CanPlaceBlueprintAt(emitter.def, cell, Rot4.North, map, thingToIgnore: emitter).Accepted) { continue; }

                best = cell;
                bestScore = score;
            }
            _tmpEmitterCells.Clear();
            return best;
        }

        private int SeedCellsAround(IntVec3 origin)
        {
            var map = Map;
            var count = 0;
            var cellCount = GenRadial.NumCellsInRadius(EmitterSeedRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = origin + GenRadial.RadialPattern[i];
                if (!Territory.InActivityRange(cell, JobGiver_ViviNestPlantSeed.OutdoorSearchRadius)) { continue; }
                if (map.fertilityGrid.FertilityAt(cell) < JobGiver_ViviNestPlantSeed.MinFertility || cell.Roofed(map)) { continue; }
                if (Territory.IsTerritoryCell(cell) || Territory.IsBoundaryCell(cell)) { continue; }
                count++;
            }
            return count;
        }
        #endregion

        #region 냉방
        public const int CellsPerCooler = 18;

        private readonly List<Room> _tmpHotRooms = new List<Room>();

        public static int MaxCoolers(Room room) => System.Math.Max(1, room.CellCount / CellsPerCooler);

        public static float CoolingStartTemperature => GenTemperature.ComfortableTemperatureRange(VVThingDefOf.VV_Vivi).max;

        public static bool IsCooler(Thing thing) => thing?.def == ThingDefOf.PassiveCooler;

        public bool NeedsCooling(Room room)
        {
            var limit = CoolingStartTemperature;
            if (room != null && room.Temperature > limit) { return true; }

            return Map.mapTemperature.SeasonalTemp + 2f * TemperatureTuning.DailyTempVariationAmplitude > limit;
        }

        private bool TryPlanCooler()
        {
            var limit = CoolingStartTemperature;
            _tmpHotRooms.Clear();
            foreach (var room in Territory.Rooms)
            {
                if (room.Temperature > limit) { _tmpHotRooms.Add(room); }
            }
            if (_tmpHotRooms.Count == 0) { return false; }

            var wood = _owner.Stock.CountOf(ThingDefOf.WoodLog) - ResourceNeeded(ThingDefOf.WoodLog);
            if (wood < CoolerWoodCost()) { return false; }

            _tmpHotRooms.SortBy(r => -r.Temperature);
            foreach (var room in _tmpHotRooms)
            {
                var coolers = CountNestCoolers(room, out var fueled);
                if (coolers >= MaxCoolers(room) || fueled < coolers) { continue; }

                foreach (var cell in WallSideRoomCells(room))
                {
                    if (TryPlace(ThingDefOf.PassiveCooler, cell, Rot4.North, null) != null) { return true; }
                }
            }
            return false;
        }

        public static int CoolerWoodCost()
        {
            var cost = 0;
            foreach (var entry in ThingDefOf.PassiveCooler.CostListAdjusted(null))
            {
                if (entry.thingDef == ThingDefOf.WoodLog) { cost += entry.count; }
            }
            return cost;
        }

        private int CountNestCoolers(Room room, out int fueled)
        {
            var map = Map;
            var count = 0;
            fueled = 0;
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (!IsCooler(building) || building.Faction != Faction || building.Position.GetRoom(map) != room) { continue; }

                count++;
                if (building.TryGetComp<CompRefuelable>()?.HasFuel ?? false) { fueled++; }
            }
            foreach (var thing in NestConstructions())
            {
                if (thing.def.entityDefToBuild == ThingDefOf.PassiveCooler && thing.Position.GetRoom(map) == room)
                {
                    count++;
                    fueled++;
                }
            }
            return count;
        }
        #endregion

        #region 초기 설비
        private bool PlanInitialFacilities()
        {
            if (TryPlacePotWithEmitter()) { return true; }

            var placed = false;
            var emitterCell = IntVec3.Invalid;

            foreach (var cell in OutdoorBuildCells().OrderByDescending(TouchesBoundaryWall))
            {
                if (TryPlace(VVThingDefOf.VV_ManaEmitter, cell, Rot4.North, null) != null)
                {
                    emitterCell = cell;
                    placed = true;
                    break;
                }
            }

            var potCells = WallSideRoomCells().ToList();
            if (emitterCell.IsValid)
            {
                potCells.SortBy(c => c.DistanceToSquared(emitterCell));
            }
            foreach (var cell in potCells)
            {
                if (TryPlace(VVThingDefOf.VV_ArcanePlantPot, cell, Rot4.North, VVThingDefOf.VV_Viviwax) != null)
                {
                    placed = true;
                    break;
                }
            }

            return placed;
        }

        private bool TryPlacePotWithEmitter()
        {
            var wallSide = new HashSet<IntVec3>(WallSideRoomCells());
            foreach (var potCell in wallSide)
            {
                foreach (var dir in GenAdj.CardinalDirections)
                {
                    var emitterCell = potCell + dir;
                    if (!wallSide.Contains(emitterCell)) { continue; }

                    var pot = TryPlace(VVThingDefOf.VV_ArcanePlantPot, potCell, Rot4.North, VVThingDefOf.VV_Viviwax);
                    if (pot == null) { break; }
                    if (TryPlace(VVThingDefOf.VV_ManaEmitter, emitterCell, Rot4.North, null) != null) { return true; }

                    pot.Destroy(DestroyMode.Cancel);
                }
            }
            return false;
        }

        private bool TouchesBoundaryWall(IntVec3 cell)
        {
            foreach (var adj in GenAdj.CardinalDirections)
            {
                var c = cell + adj;
                var edifice = c.InBounds(Map) ? c.GetEdifice(Map) : null;
                if (edifice != null && edifice.def.passability == Traversability.Impassable && Territory.IsBoundaryCell(c)) { return true; }
            }
            return false;
        }
        #endregion

        #region 선반·부화기·바닥·편자 막대
        private bool TryPlanShelf()
        {
            var roomCells = Territory.RoomCellCount;
            if (CountNestBuildings(ThingDefOf.ShelfSmall) + CountNestConstructions(ThingDefOf.ShelfSmall) >= roomCells / CellsPerShelf) { return false; }

            foreach (var cell in WallSideRoomCells())
            {
                if (TryPlace(ThingDefOf.ShelfSmall, cell, Rot4.North, VVThingDefOf.VV_Viviwax) != null) { return true; }
            }
            return false;
        }

        private bool TryPlanHatchery()
        {
            if (!_eggAllowed || !AnyRoyalReadyToLay()) { return false; }

            var hatcheries = NestHatcheries();
            if (hatcheries.Any(h => h.CanLayHere)) { return false; }

            var roomCells = Territory.RoomCellCount;
            if (hatcheries.Count >= System.Math.Min(MaxHatcheries, roomCells / CellsPerHatchery)) { return false; }

            var eggProps = VVThingDefOf.VV_ViviEgg.GetCompProperties<CompProperties_TemperatureRuinable>();
            var ideal = eggProps != null ? (eggProps.minSafeTemperature + eggProps.maxSafeTemperature) / 2f : 29f;
            foreach (var room in Territory.Rooms.OrderBy(r => System.Math.Abs(r.Temperature - ideal)))
            {
                foreach (var cell in WallSideRoomCells(room))
                {
                    if (TryPlace(VVThingDefOf.VV_ViviHatchery, cell, Rot4.South, null) != null) { return true; }
                }
            }
            return false;
        }

        private void ReleaseHatcheryOwners()
        {
            foreach (var hatchery in NestHatcheries())
            {
                var owners = hatchery.OwnersForReading;
                for (int i = owners.Count - 1; i >= 0; --i)
                {
                    if (!owners[i].DevelopmentalStage.Baby()) { owners[i].ownership?.UnclaimBed(); }
                }
            }
        }

        private List<ViviEggHatchery> NestHatcheries()
        {
            return Map.listerBuildings.allBuildingsNonColonist.OfType<ViviEggHatchery>().Where(h => h.Faction == Faction).ToList();
        }

        public int ExpectedMembers() => PendingMembers() + (_eggAllowed && AnyRoyalReadyToLay() ? 1 : 0);

        private int PendingMembers()
        {
            var count = _owner.lord?.ownedPawns.Count ?? 0;
            foreach (var hatchery in NestHatcheries())
            {
                if (hatchery.ViviEgg != null) { count++; }
            }
            return count;
        }

        private bool ComputeEggAllowed()
        {
            var pending = PendingMembers();
            if (pending + 1 > MaxMembers) { return false; }
            if (!FlowerSeason(Map) || _owner.Production.FoodReserveDays() < MinFoodDaysForEgg) { return false; }

            return (pending + 1) * RoomCellsPerMember <= Territory.RoomCellCount || !_noExpansionSite;
        }

        public static bool FlowerSeason(Map map)
        {
            var plant = ThingDefOf.Plant_Dandelion.plant;
            var seasonal = map.mapTemperature.SeasonalTemp;
            return seasonal > plant.minGrowthTemperature && seasonal < plant.maxGrowthTemperature;
        }

        private bool AnyRoyalReadyToLay()
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null) { return false; }

            foreach (var pawn in pawns)
            {
                var layer = pawn.GetCompViviEggLayer();
                if (layer != null && pawn.IsRoyalVivi() && layer.CanLayEgg) { return true; }
            }
            return false;
        }

        private bool TryPlanFloor()
        {
            if (_owner.Stock.CountOf(VVThingDefOf.VV_Viviwax) < WaxForFloor) { return false; }

            var map = Map;
            var placed = 0;
            foreach (var room in Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (placed >= FloorTilesPerProject) { return true; }

                    var terrain = map.terrainGrid.TerrainAt(cell);
                    if (terrain.IsFloor || terrain == VVTerrainDefOf.VV_HoneycombTile) { continue; }
                    if (HasNestConstructionAt(cell)) { continue; }

                    if (TryPlace(VVTerrainDefOf.VV_HoneycombTile, cell, Rot4.North, null) != null)
                    {
                        placed++;
                    }
                }
            }
            return placed > 0;
        }

        private bool TryPlanHorseshoesPin()
        {
            if (CountNestBuildings(VVThingDefOf.HorseshoesPin) > 0) { return false; }

            var idle = 0;
            var pawns = _owner.lord?.ownedPawns;
            if (pawns != null)
            {
                foreach (var pawn in pawns)
                {
                    var job = pawn.CurJobDef;
                    if (pawn.DevelopmentalStage.Adult() && (job == JobDefOf.GotoWander || job == JobDefOf.Wait_Wander || job == VVJobDefOf.VV_ViviNestNap || job == VVJobDefOf.VV_ViviNestLoaf))
                    {
                        idle++;
                    }
                }
            }
            if (idle < IdleForHorseshoes) { return false; }

            foreach (var cell in OutdoorBuildCells())
            {
                if (TryPlace(VVThingDefOf.HorseshoesPin, cell, Rot4.North, VVThingDefOf.VV_Viviwax) != null) { return true; }
            }
            return false;
        }
        #endregion

        #region 증축
        private bool TryPlanExpansion()
        {
            var pawns = _owner.lord?.ownedPawns;
            if (pawns == null || pawns.Count == 0) { return false; }
            if (Territory.RoomCellCount >= ExpectedMembers() * RoomCellsPerMember) { return false; }

            var ticks = Find.TickManager.TicksGame;
            if (_noExpansionSite && ticks < _expansionRetryTick) { return false; }

            _noExpansionSite = !TryFindExpansionSite(out var walls, out var interior, out var door);
            if (_noExpansionSite)
            {
                _expansionRetryTick = ticks + ExpansionRetryTicks;
                return false;
            }

            foreach (var cell in walls)
            {
                PlaceBoundary(cell, cell == door);
            }

            _expansionInterior = interior;
            _expansionDoor = door;
            return true;
        }

        private bool TryFinishExpansion()
        {
            if (_expansionInterior.Count == 0) { return false; }

            var map = Map;
            var room = _expansionInterior[0].GetRoom(map);
            var enclosed = room != null && !room.TouchesMapEdge && !room.IsDoorway && room.CellCount <= _expansionInterior.Count;
            if (enclosed)
            {
                Territory.AddRoom(_expansionInterior[0]);
                var center = ViviNestSiteEvaluator.CentralCell(room, map);
                Territory.AddActivityCenter(center.IsValid ? center : _expansionInterior[0]);
                if (_expansionDoor.IsValid && !_doorCells.Contains(_expansionDoor))
                {
                    _doorCells.Add(_expansionDoor);
                }
            }

            _expansionInterior.Clear();
            _expansionDoor = IntVec3.Invalid;
            return true;
        }

        private const int ExpansionRetryTicks = GenDate.TicksPerDay;
        private const float ExpansionChangeRadius = ExpansionSearchRadius + ExpansionGridMargin + 5f;
        private int _expansionRetryTick = -1;

        public void Notify_BuildingChanged(Building building)
        {
            if (_expansionRetryTick >= 0 && Territory.NearActivityCenter(building.Position, ExpansionChangeRadius))
            {
                _expansionRetryTick = -1;
            }
        }

        private static readonly bool[] ExpansionShapeOrder = { true, false };

        private bool TryFindExpansionSite(out List<IntVec3> walls, out List<IntVec3> interior, out IntVec3 door)
        {
            walls = null;
            interior = null;
            door = IntVec3.Invalid;

            var origin = EntranceCell;
            var bestScore = float.MinValue;
            var cells = Territory.ActivityCells;
            var cellCount = Territory.ActivityCellCount(ExpansionSearchRadius);
            const float minDistSq = ExpansionMinDistance * ExpansionMinDistance;
            ResetExpansionGrid();

            foreach (var hexagonal in ExpansionShapeOrder)
            {
                var bestCenter = IntVec3.Invalid;
                for (int i = 0; i < cellCount; i += ExpansionSearchStep)
                {
                    var center = cells[i];
                    if (Territory.ActivityDistSq(center) < minDistSq) { continue; }
                    if (!TryMakeShape(center, hexagonal, origin, out var shapeDoor, out var score)) { continue; }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCenter = center;
                        door = shapeDoor;
                    }
                }

                if (bestCenter.IsValid)
                {
                    var shape = ExpansionShapeOf(hexagonal);
                    walls = ShapeCells(shape.walls, bestCenter);
                    interior = ShapeCells(shape.interior, bestCenter);
                    return true;
                }
            }

            return false;
        }

        private bool TryMakeShape(IntVec3 center, bool hexagonal, IntVec3 origin, out IntVec3 door, out float score)
        {
            var map = Map;
            var shape = ExpansionShapeOf(hexagonal);
            door = IntVec3.Invalid;
            score = 0f;

            var trees = 0;
            var interior = shape.interior;
            for (int i = 0; i < interior.Length; ++i)
            {
                if (!IsExpansionCellCached(center + interior[i], ref trees)) { return false; }
            }
            var walls = shape.walls;
            for (int i = 0; i < walls.Length; ++i)
            {
                var cell = center + walls[i];
                if (!IsExpansionCellCached(cell, ref trees)) { return false; }
                if (!CanPlaceCached(cell, ExpansionWallChecked, ExpansionWallOk, VVThingDefOf.VV_ViviCreamWall, null)) { return false; }
            }

            var bestDoorDistance = float.MaxValue;
            for (int i = 0; i < shape.doors.Length; ++i)
            {
                var candidate = center + shape.doors[i];
                var outside = shape.doorOutsides[i].IsValid ? center + shape.doorOutsides[i] : IntVec3.Invalid;
                if (!outside.IsValid || !outside.InBounds(map) || !outside.Standable(map)) { continue; }
                if (!CanPlaceCached(candidate, ExpansionDoorChecked, ExpansionDoorOk, ThingDefOf.Door, VVThingDefOf.VV_Viviwax)) { continue; }

                var distance = candidate.DistanceToSquared(origin);
                if (distance >= bestDoorDistance) { continue; }
                if (!map.reachability.CanReach(origin, outside, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors))) { continue; }

                bestDoorDistance = distance;
                door = candidate;
            }
            if (!door.IsValid) { return false; }

            score = -center.DistanceTo(origin) - trees * 2f;
            return true;
        }

        private sealed class ExpansionShape
        {
            public IntVec3[] walls;
            public IntVec3[] interior;
            public IntVec3[] doors;
            public IntVec3[] doorOutsides;
        }

        private static ExpansionShape _hexagonalShape;
        private static ExpansionShape _squareShape;

        private static ExpansionShape ExpansionShapeOf(bool hexagonal)
        {
            if (hexagonal)
            {
                return _hexagonalShape ?? (_hexagonalShape = MakeExpansionShape(true));
            }
            return _squareShape ?? (_squareShape = MakeExpansionShape(false));
        }

        private static ExpansionShape MakeExpansionShape(bool hexagonal)
        {
            var center = IntVec3.Zero;
            var walls = new List<IntVec3>();
            var interior = new List<IntVec3>();
            var doorCandidates = new List<IntVec3>();
            if (hexagonal)
            {
                var rect = CellRect.CenteredOn(center, HexagonalRoomUtility.RoomCellSize.x, HexagonalRoomUtility.RoomCellSize.z);
                foreach (var c in rect.GetHexagonalEdges())
                {
                    walls.Add(new IntVec3(c.x, 0, c.z));
                }
                foreach (var c in rect.GetHexagonalFills())
                {
                    var cell = new IntVec3(c.x, 0, c.z);
                    if (!walls.Contains(cell)) { interior.Add(cell); }
                }
                doorCandidates.Add(new IntVec3(rect.minX + rect.Width / 2, 0, rect.minZ));
                doorCandidates.Add(new IntVec3(rect.minX + rect.Width / 2, 0, rect.maxZ));
            }
            else
            {
                var rect = CellRect.CenteredOn(center, 3);
                walls.AddRange(rect.EdgeCells);
                interior.AddRange(rect.ContractedBy(1).Cells);
                doorCandidates.Add(new IntVec3(rect.CenterCell.x, 0, rect.minZ));
                doorCandidates.Add(new IntVec3(rect.CenterCell.x, 0, rect.maxZ));
                doorCandidates.Add(new IntVec3(rect.minX, 0, rect.CenterCell.z));
                doorCandidates.Add(new IntVec3(rect.maxX, 0, rect.CenterCell.z));
            }

            var doors = new List<IntVec3>();
            var doorOutsides = new List<IntVec3>();
            foreach (var candidate in doorCandidates)
            {
                if (!walls.Contains(candidate)) { continue; }

                var outside = IntVec3.Invalid;
                foreach (var dir in GenAdj.CardinalDirections)
                {
                    var adj = candidate + dir;
                    if (!walls.Contains(adj) && !interior.Contains(adj)) { outside = adj; break; }
                }
                doors.Add(candidate);
                doorOutsides.Add(outside);
            }

            return new ExpansionShape
            {
                walls = walls.ToArray(),
                interior = interior.ToArray(),
                doors = doors.ToArray(),
                doorOutsides = doorOutsides.ToArray(),
            };
        }

        private static List<IntVec3> ShapeCells(IntVec3[] offsets, IntVec3 center)
        {
            var cells = new List<IntVec3>(offsets.Length);
            for (int i = 0; i < offsets.Length; ++i)
            {
                cells.Add(center + offsets[i]);
            }
            return cells;
        }

        private const int ExpansionGridMargin = 7;
        private const byte ExpansionCellChecked = 1, ExpansionCellOk = 2, ExpansionCellTree = 4, ExpansionWallChecked = 8, ExpansionWallOk = 16,
            ExpansionDoorChecked = 32, ExpansionDoorOk = 64;
        private byte[] _expansionGrid;
        private IntVec3 _expansionGridMin;
        private int _expansionGridSizeX;
        private int _expansionGridSizeZ;

        private void ResetExpansionGrid()
        {
            var half = (int)ExpansionSearchRadius + ExpansionGridMargin;
            var centers = Territory.ActivityCenters;
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            for (int i = 0; i < centers.Count; ++i)
            {
                minX = System.Math.Min(minX, centers[i].x);
                minZ = System.Math.Min(minZ, centers[i].z);
                maxX = System.Math.Max(maxX, centers[i].x);
                maxZ = System.Math.Max(maxZ, centers[i].z);
            }
            if (centers.Count == 0)
            {
                minX = maxX = _owner.NestCell.x;
                minZ = maxZ = _owner.NestCell.z;
            }

            _expansionGridMin = new IntVec3(minX - half, 0, minZ - half);
            _expansionGridSizeX = maxX - minX + half * 2 + 1;
            _expansionGridSizeZ = maxZ - minZ + half * 2 + 1;
            var length = _expansionGridSizeX * _expansionGridSizeZ;
            if (_expansionGrid == null || _expansionGrid.Length < length)
            {
                _expansionGrid = new byte[length];
            }
            else
            {
                System.Array.Clear(_expansionGrid, 0, length);
            }
        }

        private int ExpansionGridIndex(IntVec3 cell)
        {
            var x = cell.x - _expansionGridMin.x;
            var z = cell.z - _expansionGridMin.z;
            return x >= 0 && z >= 0 && x < _expansionGridSizeX && z < _expansionGridSizeZ ? x + z * _expansionGridSizeX : -1;
        }

        private bool IsExpansionCellCached(IntVec3 cell, ref int trees)
        {
            var index = ExpansionGridIndex(cell);
            if (index < 0) { return IsExpansionCell(cell, ref trees); }

            var flags = _expansionGrid[index];
            if ((flags & ExpansionCellChecked) == 0)
            {
                var cellTrees = 0;
                flags |= ExpansionCellChecked;
                if (IsExpansionCell(cell, ref cellTrees))
                {
                    flags |= ExpansionCellOk;
                    if (cellTrees > 0) { flags |= ExpansionCellTree; }
                }
                _expansionGrid[index] = flags;
            }
            if ((flags & ExpansionCellOk) == 0) { return false; }

            if ((flags & ExpansionCellTree) != 0) { trees++; }
            return true;
        }

        private bool CanPlaceCached(IntVec3 cell, byte checkedFlag, byte okFlag, BuildableDef def, ThingDef stuff)
        {
            var index = ExpansionGridIndex(cell);
            if (index < 0) { return GenConstruct.CanPlaceBlueprintAt(def, cell, Rot4.North, Map, stuffDef: stuff).Accepted; }

            var flags = _expansionGrid[index];
            if ((flags & checkedFlag) == 0)
            {
                flags |= checkedFlag;
                if (GenConstruct.CanPlaceBlueprintAt(def, cell, Rot4.North, Map, stuffDef: stuff).Accepted) { flags |= okFlag; }
                _expansionGrid[index] = flags;
            }
            return (flags & okFlag) != 0;
        }

        private bool IsExpansionCell(IntVec3 cell, ref int trees)
        {
            var map = Map;
            if (!cell.InBounds(map) || cell.InNoBuildEdgeArea(map) || cell.Fogged(map) || cell.Roofed(map)) { return false; }
            if (cell.GetEdifice(map) != null || cell.GetTerrain(map).IsWater) { return false; }
            if (map.zoneManager.ZoneAt(cell) != null) { return false; }
            if (Territory.IsTerritoryCell(cell) || Territory.IsBoundaryCell(cell) || _owner.Garden.IsCottonCell(cell)) { return false; }

            foreach (var thing in cell.GetThingList(map))
            {
                if (thing.def.category == ThingCategory.Item || BeekeepingUtility.IsStructure(thing)) { return false; }
                if (thing is Plant plant && plant.def.plant.IsTree)
                {
                    if (plant.def.plant.treeCategory == TreeCategory.Super) { return false; }
                    trees++;
                }
            }

            foreach (var adj in GenAdj.AdjacentCells)
            {
                var near = cell + adj;
                if (!near.InBounds(map)) { continue; }
                foreach (var thing in near.GetThingList(map))
                {
                    if (BeekeepingUtility.IsStructure(thing) && thing.Faction == Faction.OfPlayer) { return false; }
                }
            }

            return true;
        }
        #endregion

        #region 지붕
        public IEnumerable<IntVec3> CellsNeedingRoof()
        {
            var map = Map;
            foreach (var room in Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (!cell.Roofed(map)) { yield return cell; }
                }
            }
        }

        public static bool CanRoofNow(IntVec3 cell, Map map)
        {
            return !cell.Roofed(map) && RoofCollapseUtility.WithinRangeOfRoofHolder(cell, map) && RoofUtility.FirstBlockingThing(cell, map) == null;
        }
        #endregion

        #region 칸 고르기
        private IEnumerable<IntVec3> WallSideRoomCells()
        {
            foreach (var room in Territory.Rooms)
            {
                foreach (var cell in WallSideRoomCells(room))
                {
                    yield return cell;
                }
            }
        }

        private IEnumerable<IntVec3> WallSideRoomCells(Room room)
        {
            var map = Map;
            foreach (var cell in room.Cells)
            {
                if (IsFreeCell(cell, allowPlants: false) && BeekeepingUtility.TouchesWall(cell, map)) { yield return cell; }
            }
        }

        private IntVec3 EntranceCell => _doorCells.Count > 0 ? _doorCells[0] : _owner.NestCell;

        private IEnumerable<IntVec3> OutdoorBuildCells()
        {
            var origin = EntranceCell;
            var cellCount = GenRadial.NumCellsInRadius(OutdoorBuildRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = origin + GenRadial.RadialPattern[i];
                if (IsFreeOutdoorCell(cell, allowPlants: false)) { yield return cell; }
            }
        }

        public bool TryFindCorpseDumpCell(Pawn pawn, out IntVec3 result)
        {
            var ticks = Find.TickManager.TicksGame;
            if (ticks < _corpseDumpRetryTick)
            {
                result = IntVec3.Invalid;
                return false;
            }

            var start = Territory.ActivityCellCount(CorpseDumpMinDistance);
            var count = Territory.ActivityCellCount(CorpseDumpMaxDistance) - start;
            var offset = Rand.Range(0, count);
            if (TryFindCorpseDumpCell(pawn, start, count, offset, home: false, out result)
                || TryFindCorpseDumpCell(pawn, start, count, offset, home: true, out result))
            {
                return true;
            }

            _corpseDumpRetryTick = ticks + CorpseDumpRetryTicks;
            return false;
        }

        private bool TryFindCorpseDumpCell(Pawn pawn, int start, int count, int offset, bool home, out IntVec3 result)
        {
            var map = Map;
            var cells = Territory.ActivityCells;
            var homeArea = map.areaManager.Home;
            for (int i = 0; i < count; ++i)
            {
                var cell = cells[start + (i + offset) % count];
                if (homeArea[cell] != home) { continue; }
                if (!IsFreeOutdoorCell(cell, allowPlants: true) || !pawn.CanReserve(cell) || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some)) { continue; }

                result = cell;
                return true;
            }

            result = IntVec3.Invalid;
            return false;
        }

        public bool IsFreeOutdoorCell(IntVec3 cell, bool allowPlants, bool ignoreCotton = false)
        {
            var map = Map;
            if (!cell.InBounds(map) || cell.Roofed(map) || cell.InNoBuildEdgeArea(map)) { return false; }
            if (Territory.IsTerritoryCell(cell) || Territory.IsBoundaryCell(cell)) { return false; }
            if (!ignoreCotton && _owner.Garden.IsCottonCell(cell)) { return false; }
            if (map.zoneManager.ZoneAt(cell) != null) { return false; }
            return IsFreeCell(cell, allowPlants);
        }

        private bool IsFreeCell(IntVec3 cell, bool allowPlants)
        {
            var map = Map;
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map) || !Territory.IsClearSpot(cell)) { return false; }

            foreach (var thing in cell.GetThingList(map))
            {
                if (thing.def.category == ThingCategory.Item) { return false; }
                if (thing is Plant plant && (!allowPlants || plant.def.plant.IsTree)) { return false; }
            }

            return !_owner.Production.IsWorkSpotInteractionCell(cell);
        }
        #endregion

        #region 청사진
        public Thing TryPlace(BuildableDef def, IntVec3 cell, Rot4 rot, ThingDef stuff)
        {
            var map = Map;
            if (!GenConstruct.CanPlaceBlueprintAt(def, cell, rot, map, stuffDef: stuff).Accepted) { return null; }

            foreach (var occupied in GenAdj.OccupiedRect(cell, rot, def.Size))
            {
                if (map.zoneManager.ZoneAt(occupied) != null) { return null; }
                foreach (var thing in occupied.GetThingList(map))
                {
                    if (BeekeepingUtility.IsStructure(thing) && thing.Faction == Faction.OfPlayer) { return null; }
                }
            }

            MarkConstructionsDirty();
            return GenConstruct.PlaceBlueprintForBuild(def, cell, map, rot, Faction, stuff);
        }

        private void MarkConstructionsDirty()
        {
            _resourceNeedTick = -99999;
            _constructionCacheTick = -99999;
        }

        private bool HasNestConstructionAt(IntVec3 cell)
        {
            foreach (var thing in cell.GetThingList(Map))
            {
                if ((thing.def.IsBlueprint || thing.def.IsFrame) && thing.Faction == Faction) { return true; }
            }
            return false;
        }

        public IEnumerable<Thing> NestConstructions()
        {
            var map = Map;
            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (thing.Faction == Faction) { yield return thing; }
            }
            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (thing.Faction == Faction) { yield return thing; }
            }
        }

        public List<Thing> CachedNestConstructions()
        {
            var ticks = Find.TickManager.TicksGame;
            if (ticks - _constructionCacheTick >= ConstructionCacheInterval)
            {
                _constructionCacheTick = ticks;
                _constructionCache.Clear();
                _constructionCache.AddRange(NestConstructions());
            }
            return _constructionCache;
        }

        private bool AnyPendingProject() => NestConstructions().Any(t => !(t is Blueprint_Install));

        public int ResourceNeeded(ThingDef def)
        {
            var ticks = Find.TickManager.TicksGame;
            if (ticks - _resourceNeedTick >= ResourceNeedInterval)
            {
                _resourceNeedTick = ticks;
                _resourceNeed.Clear();
                foreach (var thing in NestConstructions())
                {
                    // Blueprint_Install에 재료를 조회하면 바닐라 오류가 발생한다.
                    if (!(thing is IConstructible constructible) || thing is Blueprint_Install) { continue; }
                    foreach (var cost in constructible.TotalMaterialCost())
                    {
                        var needed = constructible.ThingCountNeeded(cost.thingDef);
                        if (needed > 0)
                        {
                            _resourceNeed[cost.thingDef] = (_resourceNeed.TryGetValue(cost.thingDef, out var n) ? n : 0) + needed;
                        }
                    }
                }
            }
            return _resourceNeed.TryGetValue(def, out var count) ? count : 0;
        }

        private int CountNestBuildings(ThingDef def)
        {
            var count = 0;
            foreach (var building in Map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.def == def && building.Faction == Faction) { count++; }
            }
            return count;
        }

        private int CountNestConstructions(ThingDef def)
        {
            var count = 0;
            foreach (var thing in NestConstructions())
            {
                if (thing.def.entityDefToBuild == def) { count++; }
            }
            return count;
        }
        #endregion

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _doorCells, "doorCells", LookMode.Value);
            Scribe_Values.Look(ref _initialFacilitiesPlanned, "initialFacilitiesPlanned");
            Scribe_Collections.Look(ref _expansionInterior, "expansionInterior", LookMode.Value);
            Scribe_Values.Look(ref _expansionDoor, "expansionDoor", IntVec3.Invalid);
            Scribe_Values.Look(ref _eggAllowed, "eggAllowed", true);
            Scribe_Values.Look(ref _noExpansionSite, "noExpansionSite");
        }
    }
}
