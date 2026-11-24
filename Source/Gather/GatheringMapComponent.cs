using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class GatheringCellCache
    {
        public IntVec3 cell;
        public HashSet<Building_GatherWorkTable> gatherableWorkTables = new HashSet<Building_GatherWorkTable>();
        public HashSet<Thing> gatherables = new HashSet<Thing>();

        public GatheringCellCache(IntVec3 cell) { this.cell = cell; }
    }

    public class GatheringMapComponent : MapComponent
    {
        public GatheringCellCache this[IntVec3 cell]
        {
            get
            {
                if (!_cache.TryGetValue(cell, out var cache))
                {
                    cache = new GatheringCellCache(cell);
                    _cache.Add(cell, cache);
                }

                return cache;
            }
        }

        private Dictionary<IntVec3, GatheringCellCache> _cache = new Dictionary<IntVec3, GatheringCellCache>();

        private Dictionary<Building_GatherWorkTable, HashSet<GatheringCellCache>> _cacheByGatherWorkTables = new Dictionary<Building_GatherWorkTable, HashSet<GatheringCellCache>>();
        private Dictionary<Building_GatherWorkTable, HashSet<Thing>> _gatherableCacheByWorkTables = new Dictionary<Building_GatherWorkTable, HashSet<Thing>>();
        private HashSet<Building_GatherWorkTable> _dirtyGatherWorkTables = new HashSet<Building_GatherWorkTable>();

        public GatheringMapComponent(Map map) : base(map)
        {
        }

        public IEnumerable<IntVec3> GetGatherableCellsForWorkTable(Building_GatherWorkTable building)
        {
            if (_dirtyGatherWorkTables.Contains(building))
            {
                RefreshCacheForGatherWorkTable();
            }

            if (_cacheByGatherWorkTables.TryGetValue(building, out var caches))
            {
                return caches.Select(v => v.cell);
            }

            return Enumerable.Empty<IntVec3>();
        }

        public IReadOnlyCollection<Thing> GetGatherableCandidatesForWorkTable(Building_GatherWorkTable building)
        {
            if (_dirtyGatherWorkTables.Contains(building))
            {
                RefreshCacheForGatherWorkTable();
            }

            if (_gatherableCacheByWorkTables.TryGetValue(building, out var caches))
            {
                return caches;
            }

            return null;
        }

        public void Notify_WorkTableSpawned(Building_GatherWorkTable building)
        {
            _dirtyGatherWorkTables.Add(building);
            building.workTableGatherRadiusChanged += Notify_WorkTableRadiusChanged;
        }

        public void Notify_WorkTableDespawned(Building_GatherWorkTable building)
        {
            building.workTableGatherRadiusChanged -= Notify_WorkTableRadiusChanged;
            RemoveCacheForGatherWorkTable(building);

            _dirtyGatherWorkTables.Remove(building);
        }

        public void Notify_WorkTableRadiusChanged(Building_GatherWorkTable building)
        {
            RemoveCacheForGatherWorkTable(building);
            _dirtyGatherWorkTables.Add(building);
        }

        public bool HasWorkTables => _cacheByGatherWorkTables.Count > 0;

        private List<Building_GatherWorkTable> _tmpGatherWorkTables = new List<Building_GatherWorkTable>();
        public void Notify_CellRegionRebuilded(IntVec3 cell)
        {
            if (_cacheByGatherWorkTables.Count == 0) { return; }
            if (!_cache.TryGetValue(cell, out var cache) || cache.gatherableWorkTables.Count == 0) { return; }

            _tmpGatherWorkTables.Clear();
            _tmpGatherWorkTables.AddRange(cache.gatherableWorkTables);
            for (int i = 0; i < _tmpGatherWorkTables.Count; i++)
            {
                var building = _tmpGatherWorkTables[i];
                if (!_dirtyGatherWorkTables.Contains(building))
                {
                    RemoveCacheForGatherWorkTable(building);
                    _dirtyGatherWorkTables.Add(building);
                }
            }
        }

        public void Notify_GatherableSpawned(Thing thing)
        {
            var cache = this[thing.Position];
            cache.gatherables.Add(thing);
            foreach (var workTable in cache.gatherableWorkTables)
            {
                if (!_gatherableCacheByWorkTables.TryGetValue(workTable, out var gatherableCache))
                {
                    gatherableCache = new HashSet<Thing>();
                    _gatherableCacheByWorkTables.Add(workTable, gatherableCache);
                }

                gatherableCache.Add(thing);
            }
        }

        public void Notify_GatherableDespawned(Thing thing)
        {
            if (!_cache.TryGetValue(thing.Position, out var cache)) { return; }

            cache.gatherables.Remove(thing);
            foreach (var workTable in cache.gatherableWorkTables)
            {
                if (_gatherableCacheByWorkTables.TryGetValue(workTable, out var gatherableCache))
                {
                    gatherableCache.Remove(thing);
                }
            }

            if (cache.gatherables.Count == 0 && cache.gatherableWorkTables.Count == 0)
            {
                _cache.Remove(thing.Position);
            }
        }

        private readonly List<Building_GatherWorkTable> _tmpRefreshingWorkTables = new List<Building_GatherWorkTable>();
        private void RefreshCacheForGatherWorkTable()
        {
            if (_dirtyGatherWorkTables.Count == 0) { return; }

            // 아래 지역 조회가 지역 재구축을 일으키면 Notify_CellRegionRebuilded가 dirty 집합에 작업대를 추가하므로, 집합을 직접 순회하지 않고 복사본을 돈다.
            // 처리가 끝난 작업대만 집합에서 빼므로, 지역을 못 찾았거나 순회 중 추가된 작업대는 dirty로 남아 다음 호출에서 다시 처리된다.
            _tmpRefreshingWorkTables.Clear();
            _tmpRefreshingWorkTables.AddRange(_dirtyGatherWorkTables);
            for (int i = 0; i < _tmpRefreshingWorkTables.Count; i++)
            {
                var building = _tmpRefreshingWorkTables[i];
                if (building == null || !building.Spawned || building.Destroyed)
                {
                    _dirtyGatherWorkTables.Remove(building);
                    continue;
                }

                var position = building.Position;
                var radius = building.GatherRadius;
                var radiusSqr = radius * radius;

                if (!_gatherableCacheByWorkTables.TryGetValue(building, out var gatherableList))
                {
                    gatherableList = new HashSet<Thing>();
                    _gatherableCacheByWorkTables.Add(building, gatherableList);
                }
                else
                {
                    gatherableList.Clear();
                }

                RegionEntryPredicate regionEntryPredicate = (Region from, Region r) =>
                {
                    var extentsClose = r.extentsClose;
                    int dx = Mathf.Abs(position.x - Mathf.Max(extentsClose.minX, Mathf.Min(position.x, extentsClose.maxX)));
                    if (dx > radius) { return false; }

                    int dz = Mathf.Abs(position.z - Mathf.Max(extentsClose.minZ, Mathf.Min(position.z, extentsClose.maxZ)));
                    if (dz > radius) { return false; }

                    return (dx * dx + dz * dz) <= radiusSqr;
                };

                var buildingRegion = position.GetRegion(map);
                if (buildingRegion == null) { continue; }

                if (!_cacheByGatherWorkTables.TryGetValue(building, out var cacheSet))
                {
                    cacheSet = new HashSet<GatheringCellCache>();
                    _cacheByGatherWorkTables.Add(building, cacheSet);
                }

                RegionTraverser.BreadthFirstTraverse(buildingRegion, regionEntryPredicate, (r) =>
                {
                    foreach (var cell in r.Cells)
                    {
                        if (cell.DistanceToSquared(position) > radiusSqr) { continue; }

                        var cache = this[cell];
                        cache.gatherableWorkTables.Add(building);
                        if (cache.gatherables.Count > 0)
                        {
                            gatherableList.AddRange(cache.gatherables);
                        }

                        cacheSet.Add(cache);
                    }

                    return false;

                }, maxRegions: 30);

                _dirtyGatherWorkTables.Remove(building);
            }

            _tmpRefreshingWorkTables.Clear();
        }

        private void RemoveCacheForGatherWorkTable(Building_GatherWorkTable building)
        {
            if (_cacheByGatherWorkTables.TryGetValue(building, out var caches))
            {
                foreach (var cache in caches)
                {
                    cache.gatherableWorkTables.Remove(building);
                    if (cache.gatherables.Count == 0 && cache.gatherableWorkTables.Count == 0)
                    {
                        _cache.Remove(cache.cell);
                    }
                }

                _cacheByGatherWorkTables.Remove(building);
            }
        }
    }
}
