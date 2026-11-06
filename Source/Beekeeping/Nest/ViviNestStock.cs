using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 둥지가 물건을 옮겨 두는 곳(사용자 요구, 높을수록 먼저): 둥지 방 밖 정착 범위의 선반(소유 무관, 가까운 것부터) > 둥지 방 안 선반 > 둥지 방 바닥.
    // 그 밖의 칸(바깥 보관 칸 포함)으로는 옮기지 않고, 세 곳이 모두 차면 물건은 그 자리에 둔다.
    public enum ViviNestStorageRank
    {
        None = 0,
        RoomFloor = 1,
        RoomShelf = 2,
        NearShelf = 3,
    }

    // 둥지 물건 장부: 둥지 칸 위 물건과 구성원이 지닌 물건을 종류별로 센다. 보관 칸도 여기서 고른다.
    public class ViviNestStock : IExposable
    {
        public const int RefreshInterval = 250;
        // 바닐라 보관 선반(선반·작은 선반)의 묶음 태그. 유인통·호퍼·책장처럼 선반 틀을 쓰는 다른 보관 건물은 고르지 않는다.
        private const string ShelfStorageGroupTag = "Shelf";

        private static readonly List<IntVec3> _tmpCells = new List<IntVec3>();

        private readonly LordJob_ViviNest _owner;

        private readonly Dictionary<ThingDef, int> _counts = new Dictionary<ThingDef, int>();
        private float _foodNutrition;
        private int _cachedTick = -99999;

        // 둥지 칸 밖에 둔 둥지 몫(수확물·벤 나무의 원목, 둥지 방 밖 선반에 둔 물건 등). 둥지 칸에 들여오면 빠진다.
        private List<Thing> _claimed = new List<Thing>();

        public ViviNestStock(LordJob_ViviNest owner)
        {
            _owner = owner;
        }

        private Map Map => _owner.Map;
        private ViviNestTerritory Territory => _owner.Territory;

        public int CountOf(ThingDef def)
        {
            RefreshIfNeeded();
            return def != null && _counts.TryGetValue(def, out var count) ? count : 0;
        }

        // 둥지 비비가 먹을 수 있는 음식의 영양 합(식량 여유 계산용)
        public float FoodNutrition
        {
            get
            {
                RefreshIfNeeded();
                return _foodNutrition;
            }
        }

        public void MarkDirty()
        {
            _cachedTick = -99999;
        }

        private void RefreshIfNeeded()
        {
            var map = Map;
            if (map == null) { return; }

            var ticks = Find.TickManager.TicksGame;
            if (ticks - _cachedTick < RefreshInterval) { return; }
            _cachedTick = ticks;

            _counts.Clear();
            _foodNutrition = 0f;

            foreach (var cell in Territory.OwnedRoomCells)
            {
                CountCell(map, cell);
            }

            foreach (var cell in Territory.OutdoorStorageCells)
            {
                CountCell(map, cell);
            }

            // 둥지 칸 밖에 둔 둥지 몫(옮기기 전 수확물 등)도 둥지 비비가 바로 쓸 수 있다.
            PruneClaimed();
            for (int i = 0; i < _claimed.Count; ++i)
            {
                if (_claimed[i].Spawned && IsOwnedMark(_claimed[i])) { Count(_claimed[i]); }
            }

            var pawns = _owner.lord?.ownedPawns;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; ++i)
                {
                    var inventory = pawns[i].inventory?.innerContainer;
                    if (inventory != null)
                    {
                        for (int j = 0; j < inventory.Count; ++j)
                        {
                            Count(inventory[j]);
                        }
                    }

                    var carried = pawns[i].carryTracker?.CarriedThing;
                    if (carried != null)
                    {
                        Count(carried);
                    }
                }
            }
        }

        private void CountCell(Map map, IntVec3 cell)
        {
            var things = map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; ++i)
            {
                if (IsOwnedMark(things[i]))
                {
                    Count(things[i]);
                }
            }
        }

        private void Count(Thing thing)
        {
            _counts[thing.def] = (_counts.TryGetValue(thing.def, out var count) ? count : 0) + thing.stackCount;
            if (thing.def.IsNutritionGivingIngestible && thing.def.ingestible.HumanEdible && !(thing is Corpse))
            {
                _foodNutrition += thing.GetStatValue(StatDefOf.Nutrition) * thing.stackCount;
            }
        }

        public void Claim(Thing thing)
        {
            if (thing != null && !_claimed.Contains(thing))
            {
                _claimed.Add(thing);
            }
        }

        private void PruneClaimed()
        {
            _claimed.RemoveAll(t => !KeepClaim(t, saving: false));
        }

        // 둥지 비비가 들고 있는 동안에도 장부에 남겨 두어야 일이 끊겨 땅에 내려놓아도 둥지 몫으로 남는다.
        // 둥지 칸에 놓이면 장부 없이도 둥지 몫이고, 정착민 손에 들어가면 더는 둥지 몫이 아니다.
        private bool KeepClaim(Thing thing, bool saving)
        {
            if (thing == null || thing.Destroyed) { return false; }
            if (thing.Spawned) { return thing.Map == Map && !Territory.IsTerritoryCell(thing.Position); }

            var holder = thing.ParentHolder;
            if (holder == null) { return !saving; } // 막 만들어 아직 놓기 전인 산출물
            return HeldByMember(holder);
        }

        private bool HeldByMember(IThingHolder holder)
        {
            if (holder is Pawn_InventoryTracker inventory) { return inventory.pawn.GetLord() == _owner.lord; }
            if (holder is Pawn_CarryTracker carry) { return carry.pawn.GetLord() == _owner.lord; }
            return false;
        }

        // 옮길 만한 둥지 물건: 둥지 칸 밖에 둔 둥지 몫(선반에 둔 것 제외), 바깥 보관 칸에 남은 물건, 선반이 있으면 방 바닥의 물건.
        // 선반에 둔 물건은 다른 선반으로 옮기지 않는다.
        public IEnumerable<Thing> HaulCandidates()
        {
            PruneClaimed();
            for (int i = 0; i < _claimed.Count; ++i)
            {
                var thing = _claimed[i];
                if (thing.Spawned && IsOwnedMark(thing) && RankOf(thing.Position) != ViviNestStorageRank.NearShelf) { yield return thing; }
            }

            var map = Map;
            foreach (var cell in Territory.OutdoorStorageCells)
            {
                foreach (var thing in map.thingGrid.ThingsListAtFast(cell))
                {
                    if (IsOwnedMark(thing)) { yield return thing; }
                }
            }

            if (!AnyShelf) { yield break; }

            foreach (var room in Territory.Rooms)
            {
                foreach (var cell in room.Cells)
                {
                    if (RankOf(cell) != ViviNestStorageRank.RoomFloor) { continue; }
                    foreach (var thing in map.thingGrid.ThingsListAtFast(cell))
                    {
                        if (IsOwnedMark(thing)) { yield return thing; }
                    }
                }
            }
        }

        private bool AnyShelf
        {
            get
            {
                var groups = Map.haulDestinationManager.AllGroupsListForReading;
                for (int i = 0; i < groups.Count; ++i)
                {
                    if (!IsShelf(groups[i])) { continue; }

                    var cells = groups[i].CellsList;
                    for (int j = 0; j < cells.Count; ++j)
                    {
                        if (IsShelfRank(RankOf(cells[j]))) { return true; }
                    }
                }
                return false;
            }
        }

        private static bool IsShelf(SlotGroup group)
        {
            return group?.parent is Building_Storage storage && storage.Spawned && storage.def.building?.storageGroupTag == ShelfStorageGroupTag
                && storage.HaulDestinationEnabled;
        }

        public static bool IsShelfRank(ViviNestStorageRank rank) => rank == ViviNestStorageRank.NearShelf || rank == ViviNestStorageRank.RoomShelf;

        // 둥지 비비가 쓸 수 있는 물건: 둥지 칸 위에 있거나 구성원이 지녔거나 둥지 몫으로 표시한 물건
        // 둥지 몫은 언제나 금지 표시를 단다. 둥지 칸 위라도 금지되지 않은 물건(플레이어가 놓았거나 금지를 풀어 가져가려는 것)은 둥지 몫이 아니다.
        private static bool IsOwnedMark(Thing thing)
        {
            return thing.def.category == ThingCategory.Item && thing.IsForbidden(Faction.OfPlayer);
        }

        public bool IsNestOwned(Thing thing)
        {
            if (thing.Spawned)
            {
                return thing.Map == Map && IsOwnedMark(thing) && (Territory.IsTerritoryCell(thing.Position) || _claimed.Contains(thing));
            }

            return thing.ParentHolder != null && HeldByMember(thing.ParentHolder);
        }

        // 선반은 소유와 상관없이(플레이어가 일부를 거두려고 둔 선반도) 둥지 방 안이면 방 안 선반, 둥지 방 밖이면 정착 범위 안일 때만 둥지 밖 선반이다.
        public ViviNestStorageRank RankOf(IntVec3 cell)
        {
            if (!cell.IsValid) { return ViviNestStorageRank.None; }

            var room = Territory.IsRoomCell(cell);
            if (IsShelf(cell.GetSlotGroup(Map)))
            {
                if (room) { return ViviNestStorageRank.RoomShelf; }
                return Territory.InWorkRange(cell) ? ViviNestStorageRank.NearShelf : ViviNestStorageRank.None;
            }

            return room ? ViviNestStorageRank.RoomFloor : ViviNestStorageRank.None;
        }

        // 지금보다 좋은 보관 칸을 찾는다(둥지 밖 선반 > 방 안 선반 > 방 바닥). 같은 등급의 선반은 옮길 물건(지닌 것이면 지닌 비비)에서 가까운 칸부터 본다.
        public bool TryFindStorageCellFor(Thing thing, Pawn carrier, ViviNestStorageRank betterThan, out IntVec3 cell)
        {
            var map = Map;
            var origin = thing.SpawnedOrAnyParentSpawned ? thing.PositionHeld : (carrier?.Position ?? _owner.NestCell);
            for (var rank = ViviNestStorageRank.NearShelf; rank > betterThan; --rank)
            {
                CollectCandidateCells(rank, origin);
                for (int i = 0; i < _tmpCells.Count; ++i)
                {
                    if (IsValidStorageCell(_tmpCells[i], map, thing, carrier, rank))
                    {
                        cell = _tmpCells[i];
                        _tmpCells.Clear();
                        return true;
                    }
                }
            }

            _tmpCells.Clear();
            cell = IntVec3.Invalid;
            return false;
        }

        private void CollectCandidateCells(ViviNestStorageRank rank, IntVec3 origin)
        {
            _tmpCells.Clear();
            if (IsShelfRank(rank))
            {
                var groups = Map.haulDestinationManager.AllGroupsListForReading;
                for (int i = 0; i < groups.Count; ++i)
                {
                    if (!IsShelf(groups[i])) { continue; }

                    var cells = groups[i].CellsList;
                    for (int j = 0; j < cells.Count; ++j)
                    {
                        if (RankOf(cells[j]) == rank) { _tmpCells.Add(cells[j]); }
                    }
                }
                _tmpCells.SortBy(c => c.DistanceToSquared(origin));
                return;
            }

            if (rank == ViviNestStorageRank.RoomFloor)
            {
                foreach (var room in Territory.Rooms)
                {
                    foreach (var cell in room.Cells)
                    {
                        if (IsFloorStorageSpot(cell)) { _tmpCells.Add(cell); }
                    }
                }
            }
        }

        // 방 바닥 보관은 문 옆(통로)과 건물이 있는 칸을 피한다.
        private bool IsFloorStorageSpot(IntVec3 cell)
        {
            var map = Map;
            if (cell.GetEdifice(map) != null) { return false; }
            foreach (var thing in map.thingGrid.ThingsListAtFast(cell))
            {
                if (thing is Building || thing.def.IsBlueprint || thing.def.IsFrame) { return false; }
            }
            return !Territory.IsBesideBorderDoor(cell);
        }

        private bool IsValidStorageCell(IntVec3 cell, Map map, Thing thing, Pawn carrier, ViviNestStorageRank rank)
        {
            var shelf = IsShelfRank(rank);
            if (shelf)
            {
                // 선반의 보관 설정(바닐라 Accepts)과 칸 자리를 지킨다. 바닐라 놓기는 그 칸의 같은 더미에 합치고 합친 더미의 금지 상태를 따르므로
                // 둥지 몫이 아닌 같은 물건(플레이어 선반의 플레이어 더미 등)이 있는 칸은 쓰지 않는다.
                if (!cell.IsValidStorageFor(map, thing)) { return false; }
                foreach (var other in map.thingGrid.ThingsListAtFast(cell))
                {
                    if (other == thing) { return false; }
                    if (other.def == thing.def && !IsNestOwned(other)) { return false; }
                }
            }
            else
            {
                if (!cell.Standable(map)) { return false; }

                // 바닥에는 같은 종류가 쌓이는 칸이거나 빈 칸에만 둔다.
                var items = 0;
                foreach (var other in map.thingGrid.ThingsListAtFast(cell))
                {
                    if (other.def.category != ThingCategory.Item) { continue; }
                    items++;
                    if (other == thing) { return false; }
                    // 플레이어 물건(금지되지 않은 것)에 합치면 둥지 몫이 플레이어 것이 되므로 피한다.
                    if (!IsOwnedMark(other) || !other.CanStackWith(thing) || other.stackCount >= other.def.stackLimit) { return false; }
                }
                if (items > 1) { return false; }
            }

            return carrier == null || (carrier.CanReserve(cell) && carrier.CanReach(cell, shelf ? PathEndMode.ClosestTouch : PathEndMode.OnCell, Danger.Some));
        }

        // 그 칸에 더 놓을 수 있는 수(바닐라 칸 자리 계산: 같은 더미의 남은 자리 + 빈 자리 × 더미 한도). 옮기는 수를 이만큼으로 묶으면
        // 다 놓지 못한 나머지를 바닐라가 아무 비축 구역에나 다시 찾아 옮기지 않는다.
        public int StorableCount(IntVec3 cell, Thing thing) => cell.GetItemStackSpaceLeftFor(Map, thing.def);

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _claimed.RemoveAll(t => !KeepClaim(t, saving: true));
            }

            Scribe_Collections.Look(ref _claimed, "claimed", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _claimed.RemoveAll(t => t == null);
            }
        }
    }
}
