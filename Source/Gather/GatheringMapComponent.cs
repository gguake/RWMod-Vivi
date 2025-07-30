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

        public IEnumerable<Thing> GetGatherableCandidatesForWorkTable(Building_GatherWorkTable building)
        {
            if (_dirtyGatherWorkTables.Contains(building))
            {
                RefreshCacheForGatherWorkTable();
            }

            if (_gatherableCacheByWorkTables.TryGetValue(building, out var caches))
            {
                return caches;
            }

            return Enumerable.Empty<Thing>();
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

        private List<Building_GatherWorkTable> _tmpGatherWorkTables = new List<Building_GatherWorkTable>();
        public void Notify_CellRegionRebuilded(IntVec3 cell)
        {
            _tmpGatherWorkTables.AddRange(this[cell].gatherableWorkTables);
            foreach (var building in _tmpGatherWorkTables)
            {
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
            var cache = this[thing.Position];
            cache.gatherables.Remove(thing);
            foreach (var workTable in cache.gatherableWorkTables)
            {
                if (_gatherableCacheByWorkTables.TryGetValue(workTable, out var gatherableCache))
                {
                    gatherableCache.Remove(thing);
                }
            }
        }

        private void RefreshCacheForGatherWorkTable()
        {
            if (_dirtyGatherWorkTables.Count == 0) { return; }

            foreach (var building in _dirtyGatherWorkTables)
            {
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

                var centerCell = building.CenterCell;
                var buildingRegion = centerCell.GetRegion(map);
                RegionTraverser.BreadthFirstTraverse(buildingRegion, regionEntryPredicate, (r) =>
                {
                    foreach (var cell in r.Cells)
                    {
                        if (cell.DistanceToSquared(centerCell) > radiusSqr) { continue; }

                        var cache = this[cell];
                        cache.gatherableWorkTables.Add(building);
                        gatherableList.AddRange(cache.gatherables);

                        if (!_cacheByGatherWorkTables.TryGetValue(building, out var cacheSet))
                        {
                            cacheSet = new HashSet<GatheringCellCache>();
                            _cacheByGatherWorkTables.Add(building, cacheSet);
                        }
                        cacheSet.Add(cache);
                    }

                    return false;

                }, maxRegions: 30);
            }

            _dirtyGatherWorkTables.Clear();
        }

        private void RemoveCacheForGatherWorkTable(Building_GatherWorkTable building)
        {
            if (_cacheByGatherWorkTables.TryGetValue(building, out var caches))
            {
                foreach (var cache in caches)
                {
                    cache.gatherableWorkTables.Remove(building);
                }

                _cacheByGatherWorkTables.Remove(building);
            }
        }
    }
}
