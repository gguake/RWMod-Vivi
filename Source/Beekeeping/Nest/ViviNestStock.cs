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

        // 운반 사고 한 번(BeginHaulScan 뒤 그 사고의 TryFindHaulStorageCell들)에 쓰는 등급별 후보 칸: 모은 순서 그대로(정렬 전), 등급 값으로 찾는다.
        // 선반 배치·둥지 영역은 사고 한 번 안에서 바뀌지 않으므로 운반 후보마다 다시 모으지 않는다. 등급마다 처음 필요할 때(원래 처음 모으던 때) 모은다.
        private readonly List<IntVec3>[] _haulScanCells = { new List<IntVec3>(), new List<IntVec3>(), new List<IntVec3>(), new List<IntVec3>() };
        private readonly bool[] _haulScanCollected = new bool[4];

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

        // 그 필터가 받는 물건이 장부에 하나라도 있는지(연료 보충: 연료 필터)
        public bool AnyAllowedBy(ThingFilter filter)
        {
            RefreshIfNeeded();
            foreach (var pair in _counts)
            {
                if (pair.Value > 0 && filter.Allows(pair.Key)) { return true; }
            }
            return false;
        }

        // 재료 하나만 받는 레시피(옷처럼 재료로 만드는 것)에 쓸 수 있는 물건 수. 옷은 천뿐 아니라 가죽 등 그 옷이 받는 재료를 모두 센다.
        public int CountUsableFor(RecipeDef recipe)
        {
            RefreshIfNeeded();
            var count = 0;
            foreach (var pair in _counts)
            {
                if (pair.Value > 0 && IsUsableIngredient(recipe, pair.Key)) { count += pair.Value; }
            }
            return count;
        }

        // 그 레시피를 몇 번 만들 수 있는지. 바닐라 제작은 재료를 섞지 않으므로(allowMixingIngredients가 아니면 한 종류로만 채운다) 재료 종류마다 따로 나눠 더한다.
        public int BatchesFor(RecipeDef recipe)
        {
            RefreshIfNeeded();
            var batches = 0;
            foreach (var pair in _counts)
            {
                if (pair.Value <= 0 || !IsUsableIngredient(recipe, pair.Key)) { continue; }

                var required = recipe.ingredients[0].CountRequiredOfFor(pair.Key, recipe);
                if (required > 0) { batches += pair.Value / required; }
            }
            return batches;
        }

        private static bool IsUsableIngredient(RecipeDef recipe, ThingDef def)
        {
            return recipe.ingredients.Count == 1 && recipe.ingredients[0].filter.Allows(def)
                && (recipe.defaultIngredientFilter == null || recipe.defaultIngredientFilter.Allows(def));
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
        // 시체는 둥지 몫이 아니다(MarkOwned와 같다). 바닐라는 집 구역 밖에서 죽은 폰의 시체를 금지하므로, 둥지 몫으로 치면 바깥 보관 칸의 시체를
        // 운반이 둥지 방으로 들여 시체 치우기가 둥지 방 밖 시체를 건드리지 않는다는 규칙(사용자 지시)이 깨진다.
        private static bool IsOwnedMark(Thing thing)
        {
            return thing.def.category == ThingCategory.Item && !(thing is Corpse) && thing.IsForbidden(Faction.OfPlayer);
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
            return TryFindStorageCell(thing, carrier, betterThan, haulScan: false, out cell);
        }

        // 운반 사고(JobGiver_ViviNestHaul) 시작: 지난 사고에서 모은 등급별 후보 칸을 버린다. 여기서는 아무것도 모으지 않는다.
        public void BeginHaulScan()
        {
            for (int i = 0; i < _haulScanCollected.Length; ++i)
            {
                _haulScanCollected[i] = false;
                _haulScanCells[i].Clear();
            }
        }

        // TryFindStorageCellFor와 같은 칸을 고른다. 다만 등급별 후보 칸은 BeginHaulScan 뒤 그 등급을 처음 볼 때 모은 것(모은 순서 그대로)을 다시 쓰고,
        // 후보마다 그 사본을 같은 기준으로 정렬한다(같은 입력 순서·같은 비교라 정렬 결과도 같다).
        public bool TryFindHaulStorageCell(Thing thing, Pawn carrier, ViviNestStorageRank betterThan, out IntVec3 cell)
        {
            return TryFindStorageCell(thing, carrier, betterThan, haulScan: true, out cell);
        }

        private bool TryFindStorageCell(Thing thing, Pawn carrier, ViviNestStorageRank betterThan, bool haulScan, out IntVec3 cell)
        {
            var map = Map;
            var origin = thing.SpawnedOrAnyParentSpawned ? thing.PositionHeld : (carrier?.Position ?? _owner.NestCell);
            for (var rank = ViviNestStorageRank.NearShelf; rank > betterThan; --rank)
            {
                if (haulScan)
                {
                    var index = (int)rank;
                    if (!_haulScanCollected[index])
                    {
                        CollectCandidateCells(rank, _haulScanCells[index]);
                        _haulScanCollected[index] = true;
                    }
                    _tmpCells.Clear();
                    _tmpCells.AddRange(_haulScanCells[index]);
                }
                else
                {
                    CollectCandidateCells(rank, _tmpCells);
                }

                if (IsShelfRank(rank))
                {
                    _tmpCells.SortBy(c => c.DistanceToSquared(origin));
                }

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

        // 그 등급의 보관 후보 칸을 into에 모은다(정렬 전: 선반은 보관 묶음·칸 순서, 방 바닥은 방·칸 순서 그대로).
        private void CollectCandidateCells(ViviNestStorageRank rank, List<IntVec3> into)
        {
            into.Clear();
            if (IsShelfRank(rank))
            {
                var groups = Map.haulDestinationManager.AllGroupsListForReading;
                for (int i = 0; i < groups.Count; ++i)
                {
                    if (!IsShelf(groups[i])) { continue; }

                    var cells = groups[i].CellsList;
                    for (int j = 0; j < cells.Count; ++j)
                    {
                        if (RankOf(cells[j]) == rank) { into.Add(cells[j]); }
                    }
                }
                return;
            }

            if (rank == ViviNestStorageRank.RoomFloor)
            {
                foreach (var room in Territory.Rooms)
                {
                    foreach (var cell in room.Cells)
                    {
                        // 방 바닥 보관은 문 옆(통로)과 건물이 있는 칸을 피한다.
                        if (Territory.IsClearSpot(cell)) { into.Add(cell); }
                    }
                }
            }
        }

        private bool IsValidStorageCell(IntVec3 cell, Map map, Thing thing, Pawn carrier, ViviNestStorageRank rank)
        {
            // 불타는 칸은 쓰지 않는다(바닐라 IsGoodStoreCell과 같다). 바닐라 Accepts는 불을 보지 않는데 운반 잡(HaulToCell)은 불타는 목적지에서 바로 실패해,
            // 다른 구성원이 불을 끄는 동안 같은 칸을 골라 실패하기를 되풀이하고 짐 풀기는 불길 속에 물건을 놓았다.
            if (cell.ContainsStaticFire(map)) { return false; }

            var shelf = IsShelfRank(rank);
            if (shelf)
            {
                // 선반의 보관 설정(바닐라 Accepts)과 칸 자리를 지킨다. 바닐라 놓기는 그 칸의 같은 더미에 합치고 합친 더미의 금지 상태를 따르므로
                // 둥지 몫이 아닌 같은 물건(플레이어 선반의 플레이어 더미 등)이 있는 칸은 쓰지 않는다.
                if (!cell.IsValidStorageFor(map, thing)) { return false; }
                if ((thing.Spawned && thing.Map == map && thing.Position == cell) || HasForeignStack(cell, thing.def)) { return false; }
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

        // 그 칸에 옮길 수: 그 물건 수와 그 칸에 더 놓을 수 있는 수(바닐라 칸 자리 계산: 같은 더미의 남은 자리 + 빈 자리 × 더미 한도) 중 작은 것.
        // 옮기는 수를 이만큼으로 묶으면 다 놓지 못한 나머지를 바닐라가 아무 비축 구역에나 다시 찾아 옮기지 않는다.
        public int StorableCount(IntVec3 cell, Thing thing) => UnityEngine.Mathf.Min(thing.stackCount, cell.GetItemStackSpaceLeftFor(Map, thing.def));

        // 그 칸에 둥지 몫이 아닌 같은 물건 더미(플레이어 더미 등)가 있는지. 바닐라 놓기는 그 칸의 같은 더미에 합치고 합친 더미의 금지 상태를 따르므로
        // 그런 칸에 놓으면 둥지 몫이 사라진다(선반 보관 칸 고르기·둥지 몫 놓기 LordJob_ViviNest.TryPlaceOwned가 피한다).
        public bool HasForeignStack(IntVec3 cell, ThingDef def)
        {
            var things = Map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; ++i)
            {
                if (things[i].def == def && !IsNestOwned(things[i])) { return true; }
            }
            return false;
        }

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
