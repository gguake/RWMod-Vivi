using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지가 차지한 방들과 바깥 보관 칸. 방은 벽이 바뀌면 다시 만들어지므로 대표 칸만 저장하고 그때그때 방을 찾는다.
    public class ViviNestTerritory : IExposable
    {
        public const int RefreshInterval = 250;
        private const int OutdoorStorageCellCount = 12;
        private const float OutdoorStorageSearchRadius = 6f;

        private static float _workRadius = -1f;

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

        // 칸 검사(바깥 빈 땅·바닥 보관 칸·작업 자리 칸)가 쓰는 경계 칸·경계 문·문 둘레 칸(저장하지 않는다). 예전에는 칸 하나를 볼 때마다
        // 경계 칸 전체에서 문을 다시 찾아(반복기 할당 포함) 바깥 빈 땅 검사가 칸당 0.6~0.95 µs였다(실측, 경계 칸 약 50).
        // 경계 칸은 방을 더할 때만 바뀌고, 문 목록은 영역 갱신·방 추가 때와 맵의 문이 서거나 없어질 때(둥지 토일 알림) 다시 만든다.
        private readonly HashSet<IntVec3> _boundaryCellSet = new HashSet<IntVec3>();
        private readonly List<Building_Door> _borderDoors = new List<Building_Door>();
        private readonly HashSet<IntVec3> _borderDoorAdjacentCells = new HashSet<IntVec3>();
        private bool _borderDoorsDirty = true;

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

        // 정착 범위: 무리가 오가는 반경. 둥지 생활 듀티가 일을 둥지 중심에서 이 반경 안으로 묶는다(ThinkNode_ForbidOutsideFlagRadius, 24칸).
        public static float WorkRadius
        {
            get
            {
                if (_workRadius < 0f)
                {
                    _workRadius = FlagRadius(VVDutyDefOf.VV_ViviNestLife.thinkNode);
                }
                return _workRadius;
            }
        }

        public bool InWorkRange(IntVec3 cell) => cell.InHorDistOf(_owner.NestCell, WorkRadius);

        private static float FlagRadius(ThinkNode node)
        {
            if (node is ThinkNode_ForbidOutsideFlagRadius flag) { return flag.maxDistToSquadFlag; }
            if (node?.subNodes == null) { return 0f; }

            foreach (var sub in node.subNodes)
            {
                var radius = FlagRadius(sub);
                if (radius > 0f) { return radius; }
            }
            return 0f;
        }

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
                if (border.InBounds(Map) && _boundaryCellSet.Add(border))
                {
                    _boundaryCells.Add(border);
                }
            }

            _cachedTick = -99999;
            _borderDoorsDirty = true;
        }

        public bool IsBoundaryCell(IntVec3 cell) => _boundaryCellSet.Contains(cell);

        // 경계 칸 위의 문(경계 칸 순서, 여러 칸 문은 칸마다 한 번씩)
        public IReadOnlyList<Building_Door> BorderDoors
        {
            get
            {
                EnsureBorderDoors();
                return _borderDoors;
            }
        }

        // 경계 문 칸이거나 그 8방향 이웃(통로)
        public bool IsBesideBorderDoor(IntVec3 cell)
        {
            EnsureBorderDoors();
            return _borderDoorAdjacentCells.Contains(cell);
        }

        // 맵의 건물이 서거나 없어졌다(둥지 토일 알림). 문이면 다음 칸 검사 때 문 목록을 다시 만든다.
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

        private void PickOutdoorStorageCells()
        {
            _outdoorStorageCells.Clear();

            var doors = BorderDoors;
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
            if (_boundaryCellSet.Contains(cell) || IsRoomCell(cell)) { return false; }

            var room = cell.GetRoom(map);
            if (room == null || !room.PsychologicallyOutdoors) { return false; }
            if (map.zoneManager.ZoneAt(cell) != null) { return false; }
            if (cell.GetEdifice(map) != null) { return false; }

            foreach (var thing in cell.GetThingList(map))
            {
                if (thing.def.IsBlueprint || thing.def.IsFrame || thing is Building) { return false; }
            }

            return !IsBesideBorderDoor(cell);
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
                _boundaryCellSet.Clear();
                _boundaryCellSet.UnionWith(_boundaryCells);
            }
        }
    }
}
