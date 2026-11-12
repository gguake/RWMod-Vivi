using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ViviNestTerritory : IExposable
    {
        public const int RefreshInterval = 250;
        private const int OutdoorStorageCellCount = 12;
        private const float OutdoorStorageSearchRadius = 6f;

        private readonly LordJob_ViviNest _owner;

        private List<IntVec3> _roomCells = new List<IntVec3>();
        private List<IntVec3> _boundaryCells = new List<IntVec3>();
        private List<IntVec3> _outdoorStorageCells = new List<IntVec3>();
        private List<IntVec3> _ownedRoomCells = new List<IntVec3>();

        private readonly HashSet<IntVec3> _cachedRoomCells = new HashSet<IntVec3>();
        private readonly HashSet<IntVec3> _ownedRoomCellSet = new HashSet<IntVec3>();
        private readonly List<Room> _cachedRooms = new List<Room>();
        private int _cachedTick = -99999;

        private readonly HashSet<IntVec3> _boundaryCellSet = new HashSet<IntVec3>();
        private readonly List<Building_Door> _borderDoors = new List<Building_Door>();
        private readonly HashSet<IntVec3> _borderDoorAdjacentCells = new HashSet<IntVec3>();
        private bool _borderDoorsDirty = true;

        public IReadOnlyList<IntVec3> BoundaryCells => _boundaryCells;
        public IReadOnlyList<IntVec3> OutdoorStorageCells => _outdoorStorageCells;

        // GenDraw에 전달하므로 List를 유지한다.
        public List<IntVec3> OwnedRoomCellsForDrawing
        {
            get
            {
                RefreshIfNeeded();
                return _ownedRoomCells;
            }
        }

        public IReadOnlyList<IntVec3> OwnedRoomCells
        {
            get
            {
                RefreshIfNeeded();
                return _ownedRoomCells;
            }
        }

        public ViviNestTerritory(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;

        public IReadOnlyList<Room> Rooms
        {
            get
            {
                RefreshIfNeeded();
                return _cachedRooms;
            }
        }

        public int RoomCellCount
        {
            get
            {
                RefreshIfNeeded();
                var count = 0;
                for (int i = 0; i < _cachedRooms.Count; ++i)
                {
                    count += _cachedRooms[i].CellCount;
                }
                return count;
            }
        }

        public bool IsRoomCell(IntVec3 cell)
        {
            RefreshIfNeeded();
            return _cachedRoomCells.Contains(cell);
        }

        public bool IsOutdoorStorageCell(IntVec3 cell) => _outdoorStorageCells.Contains(cell);

        public bool IsNestRoomCell(IntVec3 cell)
        {
            RefreshIfNeeded();
            return _ownedRoomCellSet.Contains(cell);
        }

        public bool IsTerritoryCell(IntVec3 cell)
        {
            RefreshIfNeeded();
            return _ownedRoomCellSet.Contains(cell) || _outdoorStorageCells.Contains(cell);
        }

        #region 활동 범위
        public const float WorkRadius = 24f;
        private const ushort OutsideActivity = ushort.MaxValue;

        private List<IntVec3> _activityCenters = new List<IntVec3>();
        private ushort[] _activityDistSq;
        private readonly List<IntVec3> _activityCells = new List<IntVec3>();

        public IReadOnlyList<IntVec3> ActivityCenters => _activityCenters;

        public IReadOnlyList<IntVec3> ActivityCells
        {
            get
            {
                EnsureActivityGrid();
                return _activityCells;
            }
        }

        // GenDraw에 전달하므로 List를 유지한다.
        public List<IntVec3> ActivityCellsForDrawing
        {
            get
            {
                EnsureActivityGrid();
                return _activityCells;
            }
        }

        public void AddActivityCenter(IntVec3 center)
        {
            if (_activityCenters.Contains(center)) { return; }

            _activityCenters.Add(center);
            if (_activityDistSq != null)
            {
                PaintActivity(center);
            }
        }

        public int ActivityDistSq(IntVec3 cell)
        {
            EnsureActivityGrid();
            var map = Map;
            if (_activityDistSq == null || !cell.InBounds(map)) { return int.MaxValue; }

            var distSq = _activityDistSq[map.cellIndices.CellToIndex(cell)];
            return distSq == OutsideActivity ? int.MaxValue : distSq;
        }

        public bool InActivityRange(IntVec3 cell, float radius) => ActivityDistSq(cell) <= radius * radius;

        public bool InWorkRange(IntVec3 cell) => InActivityRange(cell, WorkRadius);

        public int ActivityCellCount(float radius)
        {
            EnsureActivityGrid();
            var limit = radius * radius;
            int lo = 0, hi = _activityCells.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (ActivityDistSq(_activityCells[mid]) <= limit) { lo = mid + 1; }
                else { hi = mid; }
            }
            return lo;
        }

        public bool NearActivityCenter(IntVec3 cell, float radius)
        {
            for (int i = 0; i < _activityCenters.Count; ++i)
            {
                if (cell.InHorDistOf(_activityCenters[i], radius)) { return true; }
            }
            return false;
        }

        public float ActivityReachFrom(IntVec3 root)
        {
            var reach = 0f;
            for (int i = 0; i < _activityCenters.Count; ++i)
            {
                reach = System.Math.Max(reach, (_activityCenters[i] - root).LengthHorizontal + WorkRadius);
            }
            return reach;
        }

        private void EnsureActivityGrid()
        {
            if (_activityDistSq != null) { return; }

            var map = Map;
            if (map == null) { return; }

            _activityDistSq = new ushort[map.cellIndices.NumGridCells];
            for (int i = 0; i < _activityDistSq.Length; ++i)
            {
                _activityDistSq[i] = OutsideActivity;
            }
            _activityCells.Clear();
            for (int i = 0; i < _activityCenters.Count; ++i)
            {
                PaintActivity(_activityCenters[i]);
            }
        }

        private void PaintActivity(IntVec3 center)
        {
            var map = Map;
            var indices = map.cellIndices;
            var cellCount = GenRadial.NumCellsInRadius(WorkRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var offset = GenRadial.RadialPattern[i];
                var cell = center + offset;
                if (!cell.InBounds(map)) { continue; }

                var index = indices.CellToIndex(cell);
                var distSq = (ushort)offset.LengthHorizontalSquared;
                var old = _activityDistSq[index];
                if (distSq >= old) { continue; }

                if (old == OutsideActivity)
                {
                    _activityCells.Add(cell);
                }
                _activityDistSq[index] = distSq;
            }

            _activityCells.SortStable((a, b) => _activityDistSq[indices.CellToIndex(a)].CompareTo(_activityDistSq[indices.CellToIndex(b)]));
        }
        #endregion

        private static readonly List<Room> _tmpStructure = new List<Room>();

        public void Establish(IntVec3 nestCell)
        {
            AddStructure(nestCell);
            AddActivityCenter(nestCell);

            _outdoorStorageCells.Clear();
            var door = EntranceDoor;
            var origin = door != null ? door.Position : _roomCells.FirstOrDefault();
            var cellCount = GenRadial.NumCellsInRadius(OutdoorStorageSearchRadius);
            for (int i = 0; i < cellCount && _outdoorStorageCells.Count < OutdoorStorageCellCount; ++i)
            {
                var cell = origin + GenRadial.RadialPattern[i];
                if (IsUsableOutdoorStorageCell(cell))
                {
                    _outdoorStorageCells.Add(cell);
                }
            }
        }

        private void AddStructure(IntVec3 nestCell)
        {
            var map = Map;
            var nestRoom = nestCell.GetRoom(map);
            ViviNestSiteEvaluator.CollectStructureRooms(nestRoom, _tmpStructure);
            if (_tmpStructure.Count > 1 && ViviNestSiteEvaluator.StructureScore(_tmpStructure, map, out _, out _) > 0f)
            {
                foreach (var room in _tmpStructure)
                {
                    var cell = room == nestRoom ? nestCell : ViviNestSiteEvaluator.CentralCell(room, map);
                    AddRoom(cell.IsValid ? cell : room.Cells.First());
                }
            }
            else
            {
                AddRoom(nestCell);
            }
            _tmpStructure.Clear();
        }

        public void AddRoom(IntVec3 roomCell)
        {
            var room = roomCell.GetRoom(Map);
            if (room == null || room.PsychologicallyOutdoors) { return; }

            if (!_roomCells.Contains(roomCell))
            {
                _roomCells.Add(roomCell);
            }

            foreach (var border in room.BorderCells)
            {
                if (border.InBounds(Map) && _boundaryCellSet.Add(border))
                {
                    _boundaryCells.Add(border);
                }
            }

            _cachedTick = -99999;
            _borderDoorsDirty = true;
        }

        public bool IsBoundaryCell(IntVec3 cell) => _boundaryCellSet.Contains(cell);

        public IReadOnlyList<Building_Door> BorderDoors
        {
            get
            {
                EnsureBorderDoors();
                return _borderDoors;
            }
        }

        public bool IsOuterDoor(Building_Door door)
        {
            var map = Map;
            foreach (var cell in door.OccupiedRect())
            {
                foreach (var adj in GenAdj.CardinalDirections)
                {
                    var c = cell + adj;
                    var room = c.InBounds(map) ? c.GetRoom(map) : null;
                    if (room != null && !room.IsDoorway && ViviNestSiteEvaluator.IsOutside(room)) { return true; }
                }
            }
            return false;
        }

        public Building_Door EntranceDoor
        {
            get
            {
                var doors = BorderDoors;
                for (int i = 0; i < doors.Count; ++i)
                {
                    if (IsOuterDoor(doors[i])) { return doors[i]; }
                }
                return doors.Count > 0 ? doors[0] : null;
            }
        }

        public bool IsBesideBorderDoor(IntVec3 cell)
        {
            EnsureBorderDoors();
            return _borderDoorAdjacentCells.Contains(cell);
        }

        public bool IsClearSpot(IntVec3 cell)
        {
            var map = Map;
            if (cell.GetEdifice(map) != null) { return false; }
            foreach (var thing in map.thingGrid.ThingsListAtFast(cell))
            {
                if (BeekeepingUtility.IsStructure(thing)) { return false; }
            }
            return !IsBesideBorderDoor(cell);
        }

        public void Notify_BuildingChanged(Building building)
        {
            if (building is Building_Door) { _borderDoorsDirty = true; }
        }

        private void EnsureBorderDoors()
        {
            RefreshIfNeeded();
            if (!_borderDoorsDirty) { return; }

            var map = Map;
            if (map == null) { return; }

            _borderDoorsDirty = false;
            _borderDoors.Clear();
            _borderDoorAdjacentCells.Clear();
            for (int i = 0; i < _boundaryCells.Count; ++i)
            {
                var door = _boundaryCells[i].GetDoor(map);
                if (door == null) { continue; }

                _borderDoors.Add(door);
                foreach (var offset in GenAdj.AdjacentCellsAndInside)
                {
                    _borderDoorAdjacentCells.Add(door.Position + offset);
                }
            }
        }

        private bool IsUsableOutdoorStorageCell(IntVec3 cell)
        {
            var map = Map;
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)) { return false; }
            if (_boundaryCellSet.Contains(cell) || IsRoomCell(cell)) { return false; }

            var room = cell.GetRoom(map);
            if (room == null || !room.PsychologicallyOutdoors) { return false; }
            if (map.zoneManager.ZoneAt(cell) != null) { return false; }

            return IsClearSpot(cell);
        }

        private void RefreshIfNeeded()
        {
            var map = Map;
            if (map == null) { return; }

            var ticks = Find.TickManager.TicksGame;
            if (ticks - _cachedTick < RefreshInterval) { return; }
            _cachedTick = ticks;
            _borderDoorsDirty = true;

            _cachedRooms.Clear();
            _cachedRoomCells.Clear();
            var anyBreached = false;
            for (int i = 0; i < _roomCells.Count; ++i)
            {
                var room = _roomCells[i].GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors || room.IsDoorway)
                {
                    anyBreached = true;
                    continue;
                }
                if (_cachedRooms.Contains(room)) { continue; }

                _cachedRooms.Add(room);
                foreach (var cell in room.Cells)
                {
                    _cachedRoomCells.Add(cell);
                }
            }

            if (!anyBreached)
            {
                _ownedRoomCellSet.Clear();
            }
            _ownedRoomCellSet.UnionWith(_cachedRoomCells);
            _ownedRoomCells.Clear();
            _ownedRoomCells.AddRange(_ownedRoomCellSet);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _roomCells, "roomCells", LookMode.Value);
            Scribe_Collections.Look(ref _boundaryCells, "boundaryCells", LookMode.Value);
            Scribe_Collections.Look(ref _outdoorStorageCells, "outdoorStorageCells", LookMode.Value);
            Scribe_Collections.Look(ref _ownedRoomCells, "ownedRoomCells", LookMode.Value);
            Scribe_Collections.Look(ref _activityCenters, "activityCenters", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _ownedRoomCellSet.Clear();
                _ownedRoomCellSet.UnionWith(_ownedRoomCells);
                _boundaryCellSet.Clear();
                _boundaryCellSet.UnionWith(_boundaryCells);
            }
        }
    }
}
