using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 둥지가 차지한 방들과 바깥 보관 칸. 방은 벽이 바뀌면 다시 만들어지므로 대표 칸만 저장하고 그때그때 방을 찾는다.
    public class ViviNestTerritory : IExposable
    {
        public const int RefreshInterval = 250;
        private const int OutdoorStorageCellCount = 12;
        private const float OutdoorStorageSearchRadius = 6f;

        private readonly LordJob_ViviNest _owner;

        private List<IntVec3> _roomCells = new List<IntVec3>();
        private List<IntVec3> _boundaryCells = new List<IntVec3>();
        private List<IntVec3> _outdoorStorageCells = new List<IntVec3>();
        // 마지막으로 온전했던 둥지 방 칸. 벽이 뚫려 방이 바깥과 이어진 동안에도 이 칸의 물건은 둥지 몫으로 둔다.
        private List<IntVec3> _ownedRoomCells = new List<IntVec3>();

        private readonly HashSet<IntVec3> _cachedRoomCells = new HashSet<IntVec3>();
        private readonly HashSet<IntVec3> _ownedRoomCellSet = new HashSet<IntVec3>();
        private readonly List<Room> _cachedRooms = new List<Room>();
        private int _cachedTick = -99999;

        public IReadOnlyList<IntVec3> BoundaryCells => _boundaryCells;
        public IReadOnlyList<IntVec3> OutdoorStorageCells => _outdoorStorageCells;

        // 테두리 그리기(GenDraw)가 List를 받으므로 그대로 넘긴다. 고치지 말 것.
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

        public bool IsRoomCell(IntVec3 cell)
        {
            RefreshIfNeeded();
            return _cachedRoomCells.Contains(cell);
        }

        public bool IsOutdoorStorageCell(IntVec3 cell) => _outdoorStorageCells.Contains(cell);

        // 둥지 방 칸(벽이 뚫린 동안은 원래 방 칸). 정착민이 여기 들어오면 경계한다.
        public bool IsNestRoomCell(IntVec3 cell)
        {
            RefreshIfNeeded();
            return _ownedRoomCellSet.Contains(cell);
        }

        // 둥지 물건으로 치는 칸: 둥지 방 안(벽이 뚫린 동안은 원래 방 칸)과 바깥 보관 칸
        public bool IsTerritoryCell(IntVec3 cell)
        {
            RefreshIfNeeded();
            return _ownedRoomCellSet.Contains(cell) || _outdoorStorageCells.Contains(cell);
        }

        public bool HasAnyRoom => Rooms.Count > 0;

        // 정착할 때 방의 경계(벽·문)를 기억해 두고, 방 밖 가까운 빈 땅에 보관 칸을 잡는다.
        public void Establish(IntVec3 nestCell)
        {
            AddRoom(nestCell);
            PickOutdoorStorageCells();
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
                if (border.InBounds(Map) && !_boundaryCells.Contains(border))
                {
                    _boundaryCells.Add(border);
                }
            }

            _cachedTick = -99999;
        }

        public bool IsBorderDoor(Building_Door door) => door.Spawned && _boundaryCells.Contains(door.Position);

        public IEnumerable<Building_Door> BorderDoors
        {
            get
            {
                foreach (var cell in _boundaryCells)
                {
                    var door = cell.GetDoor(Map);
                    if (door != null)
                    {
                        yield return door;
                    }
                }
            }
        }

        private void PickOutdoorStorageCells()
        {
            _outdoorStorageCells.Clear();

            var doors = BorderDoors.ToList();
            var origin = doors.Count > 0 ? doors[0].Position : _roomCells.FirstOrDefault();
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

        // 문 바로 앞(통로)은 비우고, 플레이어 구역·건물·청사진과 겹치지 않는 바깥 빈 땅
        private bool IsUsableOutdoorStorageCell(IntVec3 cell)
        {
            var map = Map;
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)) { return false; }
            if (_boundaryCells.Contains(cell) || IsRoomCell(cell)) { return false; }

            var room = cell.GetRoom(map);
            if (room == null || !room.PsychologicallyOutdoors) { return false; }
            if (map.zoneManager.ZoneAt(cell) != null) { return false; }
            if (cell.GetEdifice(map) != null) { return false; }

            foreach (var thing in cell.GetThingList(map))
            {
                if (thing.def.IsBlueprint || thing.def.IsFrame || thing is Building) { return false; }
            }

            foreach (var door in BorderDoors)
            {
                if (cell.AdjacentTo8WayOrInside(door.Position)) { return false; }
            }

            return true;
        }

        private void RefreshIfNeeded()
        {
            var map = Map;
            if (map == null) { return; }

            var ticks = Find.TickManager.TicksGame;
            if (ticks - _cachedTick < RefreshInterval) { return; }
            _cachedTick = ticks;

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

            // 모든 방이 온전하면 지금 방 칸으로 바꾸고, 뚫린 방이 있으면 원래 칸을 남긴 채 온전한 방 칸만 더한다.
            if (!anyBreached)
            {
                _ownedRoomCellSet.Clear();
            }
            _ownedRoomCellSet.UnionWith(_cachedRoomCells);
            _ownedRoomCells.Clear();
            _ownedRoomCells.AddRange(_ownedRoomCellSet);
        }

        public void MarkDirty()
        {
            _cachedTick = -99999;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref _roomCells, "roomCells", LookMode.Value);
            Scribe_Collections.Look(ref _boundaryCells, "boundaryCells", LookMode.Value);
            Scribe_Collections.Look(ref _outdoorStorageCells, "outdoorStorageCells", LookMode.Value);
            Scribe_Collections.Look(ref _ownedRoomCells, "ownedRoomCells", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _ownedRoomCellSet.Clear();
                _ownedRoomCellSet.UnionWith(_ownedRoomCells);
            }
        }
    }
}
