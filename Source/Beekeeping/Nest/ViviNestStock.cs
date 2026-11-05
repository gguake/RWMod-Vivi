using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public enum ViviNestStorageRank
    {
        None = 0,
        Outdoor = 1,
        RoomFloor = 2,
        Shelf = 3,
    }

    // 둥지 물건 장부: 둥지 칸 위 물건과 구성원이 지닌 물건을 종류별로 센다. 보관 칸도 여기서 고른다.
    public class ViviNestStock : IExposable
    {
        public const int RefreshInterval = 250;

        private readonly LordJob_ViviNest _owner;

        private readonly Dictionary<ThingDef, int> _counts = new Dictionary<ThingDef, int>();
        private float _foodNutrition;
        private int _cachedTick = -99999;

        // 둥지 칸 밖에 떨어진 둥지 몫(수확물·벤 나무의 원목 등). 둥지 비비가 들여오면 빠진다.
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

        // 옮길 만한 둥지 물건: 바깥에 둔 둥지 몫, 바깥 보관 칸·방 바닥의 물건(더 좋은 칸이 있으면 옮긴다)
        public IEnumerable<Thing> HaulCandidates()
        {
            PruneClaimed();
            for (int i = 0; i < _claimed.Count; ++i)
            {
                if (_claimed[i].Spawned && IsOwnedMark(_claimed[i])) { yield return _claimed[i]; }
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
                foreach (var building in Map.listerBuildings.allBuildingsNonColonist)
                {
                    if (building is Building_Storage && building.Faction == _owner.Faction && Territory.IsRoomCell(building.Position))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

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

        public ViviNestStorageRank RankOf(IntVec3 cell)
        {
            if (!cell.IsValid) { return ViviNestStorageRank.None; }

            var storage = cell.GetSlotGroup(Map)?.parent as Building_Storage;
            if (storage != null && storage.Faction == _owner.Faction && Territory.IsRoomCell(cell))
            {
                return ViviNestStorageRank.Shelf;
            }
            if (Territory.IsRoomCell(cell))
            {
                return ViviNestStorageRank.RoomFloor;
            }
            if (Territory.IsOutdoorStorageCell(cell))
            {
                return ViviNestStorageRank.Outdoor;
            }

            return ViviNestStorageRank.None;
        }

        // 지금보다 좋은 보관 칸을 찾는다(선반 > 방 바닥 > 바깥 보관 칸).
        public bool TryFindStorageCellFor(Thing thing, Pawn carrier, ViviNestStorageRank betterThan, out IntVec3 cell)
        {
            var map = Map;
            for (var rank = ViviNestStorageRank.Shelf; rank > betterThan; --rank)
            {
                foreach (var candidate in CandidateCells(rank))
                {
                    if (IsValidStorageCell(candidate, map, thing, carrier, rank))
                    {
                        cell = candidate;
                        return true;
                    }
                }
            }

            cell = IntVec3.Invalid;
            return false;
        }

        private IEnumerable<IntVec3> CandidateCells(ViviNestStorageRank rank)
        {
            switch (rank)
            {
                case ViviNestStorageRank.Shelf:
                    foreach (var building in Map.listerBuildings.allBuildingsNonColonist)
                    {
                        if (building is Building_Storage storage && storage.Faction == _owner.Faction)
                        {
                            foreach (var cell in storage.AllSlotCells())
                            {
                                if (Territory.IsRoomCell(cell)) { yield return cell; }
                            }
                        }
                    }
                    break;

                case ViviNestStorageRank.RoomFloor:
                    foreach (var room in Territory.Rooms)
                    {
                        foreach (var cell in room.Cells)
                        {
                            if (IsFloorStorageSpot(cell)) { yield return cell; }
                        }
                    }
                    break;

                case ViviNestStorageRank.Outdoor:
                    foreach (var cell in Territory.OutdoorStorageCells)
                    {
                        yield return cell;
                    }
                    break;
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
            foreach (var door in Territory.BorderDoors)
            {
                if (cell.AdjacentTo8WayOrInside(door.Position)) { return false; }
            }
            return true;
        }

        private static bool IsValidStorageCell(IntVec3 cell, Map map, Thing thing, Pawn carrier, ViviNestStorageRank rank)
        {
            if (!cell.Standable(map) && rank != ViviNestStorageRank.Shelf) { return false; }
            if (rank == ViviNestStorageRank.Shelf)
            {
                if (!(cell.GetSlotGroup(map)?.parent is Building_Storage) || !cell.IsValidStorageFor(map, thing)) { return false; }
            }
            else
            {
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

            return carrier == null || (carrier.CanReserve(cell) && carrier.CanReach(cell, PathEndMode.OnCell, Danger.Some));
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
