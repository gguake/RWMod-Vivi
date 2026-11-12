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
        RoomFloor = 1,
        RoomShelf = 2,
        NearShelf = 3,
    }

    public class ViviNestStock : IExposable
    {
        public const int RefreshInterval = 250;
        private const string ShelfStorageGroupTag = "Shelf";

        private static readonly List<IntVec3> _tmpCells = new List<IntVec3>();

        private readonly LordJob_ViviNest _owner;

        private readonly List<IntVec3>[] _haulScanCells = { new List<IntVec3>(), new List<IntVec3>(), new List<IntVec3>(), new List<IntVec3>() };
        private readonly bool[] _haulScanCollected = new bool[4];

        private readonly Dictionary<ThingDef, int> _counts = new Dictionary<ThingDef, int>();
        private float _foodNutrition;
        private int _cachedTick = -99999;

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

        public bool AnyAllowedBy(ThingFilter filter)
        {
            RefreshIfNeeded();
            foreach (var pair in _counts)
            {
                if (pair.Value > 0 && filter.Allows(pair.Key)) { return true; }
            }
            return false;
        }

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

            PruneClaimed();
            for (int i = 0; i < _claimed.Count; ++i)
            {
                if (_claimed[i].Spawned && IsOwnedMark(_claimed[i]) && IsUsableClaim(_claimed[i])) { Count(_claimed[i]); }
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

        private bool KeepClaim(Thing thing, bool saving)
        {
            if (thing == null || thing.Destroyed) { return false; }
            if (thing.Spawned) { return thing.Map == Map && !Territory.IsTerritoryCell(thing.Position); }

            var holder = thing.ParentHolder;
            if (holder == null) { return !saving; }
            return HeldByMember(holder);
        }

        private bool HeldByMember(IThingHolder holder)
        {
            if (holder is Pawn_InventoryTracker inventory) { return inventory.pawn.GetLord() == _owner.lord; }
            if (holder is Pawn_CarryTracker carry) { return carry.pawn.GetLord() == _owner.lord; }
            return false;
        }

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

        private static bool IsOwnedMark(Thing thing)
        {
            return thing.def.category == ThingCategory.Item && !(thing is Corpse) && thing.IsForbidden(Faction.OfPlayer);
        }

        private bool IsUsableClaim(Thing thing) => Territory.InWorkRange(thing.Position);

        public bool IsNestOwned(Thing thing)
        {
            if (thing.Spawned)
            {
                return thing.Map == Map && IsOwnedMark(thing)
                    && (Territory.IsTerritoryCell(thing.Position) || (_claimed.Contains(thing) && IsUsableClaim(thing)));
            }

            return thing.ParentHolder != null && HeldByMember(thing.ParentHolder);
        }

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

        public bool TryFindStorageCellFor(Thing thing, Pawn carrier, ViviNestStorageRank betterThan, out IntVec3 cell)
        {
            return TryFindStorageCell(thing, carrier, betterThan, haulScan: false, out cell);
        }

        public void BeginHaulScan()
        {
            for (int i = 0; i < _haulScanCollected.Length; ++i)
            {
                _haulScanCollected[i] = false;
                _haulScanCells[i].Clear();
            }
        }

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
                        if (Territory.IsClearSpot(cell)) { into.Add(cell); }
                    }
                }
            }
        }

        private bool IsValidStorageCell(IntVec3 cell, Map map, Thing thing, Pawn carrier, ViviNestStorageRank rank)
        {
            if (cell.ContainsStaticFire(map)) { return false; }

            var shelf = IsShelfRank(rank);
            if (shelf)
            {
                if (!cell.IsValidStorageFor(map, thing)) { return false; }
                if ((thing.Spawned && thing.Map == map && thing.Position == cell) || HasForeignStack(cell, thing.def)) { return false; }
            }
            else
            {
                if (!cell.Standable(map)) { return false; }

                var items = 0;
                foreach (var other in map.thingGrid.ThingsListAtFast(cell))
                {
                    if (other.def.category != ThingCategory.Item) { continue; }
                    items++;
                    if (other == thing) { return false; }
                    if (!IsOwnedMark(other) || !other.CanStackWith(thing) || other.stackCount >= other.def.stackLimit) { return false; }
                }
                if (items > 1) { return false; }
            }

            return carrier == null || (carrier.CanReserve(cell) && carrier.CanReach(cell, shelf ? PathEndMode.ClosestTouch : PathEndMode.OnCell, Danger.Some));
        }

        public int StorableCount(IntVec3 cell, Thing thing) => UnityEngine.Mathf.Min(thing.stackCount, cell.GetItemStackSpaceLeftFor(Map, thing.def));

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
