using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public struct GatherableCandidateCache
    {
        public int cachedTicks;
        public HashSet<Thing> cachedThings;
    }

    public class GatherableCache
    {
        public const int CacheExpireTicks = 2000;

        private Map _map;
        private Dictionary<(Region, RecipeDef_Gathering), GatherableCandidateCache> _cache;

        public GatherableCache(Map map)
        {
            _map = map;
            _cache = new Dictionary<(Region, RecipeDef_Gathering), GatherableCandidateCache>();
        }

        public IEnumerable<Thing> GetGatherablesCache(Region region, RecipeDef_Gathering recipeDef)
        {
            ExpireOldCache();

            if (_cache.TryGetValue((region, recipeDef), out var cached))
            {
                return cached.cachedThings;
            }
            else
            {
                var newCache = RecacheGatherables(region, recipeDef);
                return newCache.cachedThings;
            }
        }

        public void ExpireOldCache()
        {
            _cache.RemoveAll(kv => GenTicks.TicksGame > kv.Value.cachedTicks + CacheExpireTicks);
        }

        public GatherableCandidateCache RecacheGatherables(Region region, RecipeDef_Gathering recipeDef)
        {
            var gatherWorker = recipeDef.gatherWorker;
            var gatherables = gatherWorker.FindAllGatherableTargetInRegion(region).ToHashSet();

            var result = new GatherableCandidateCache()
            {
                cachedTicks = GenTicks.TicksGame,
                cachedThings = gatherables
            };

            _cache.Remove((region, recipeDef));
            _cache.Add((region, recipeDef), result);
            return result;
        }
    }

    public class Building_GatherWorkTable : Building_WorkTable
    {
        public const float gatherRadius = 11.9f;

        private GatherableCache _gatherableCache;

        // 맵별로 모든 채집 건물은 캐시를 공유한다
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            var otherGatherWorkTable = (Building_GatherWorkTable)(
                map.listerBuildings.allBuildingsColonist.FirstOrDefault(v => v is Building_GatherWorkTable gatherWorkTable) ??
                map.listerBuildings.allBuildingsNonColonist.FirstOrDefault(v => v is Building_GatherWorkTable gatherWorkTable));

            if (otherGatherWorkTable != null && otherGatherWorkTable._gatherableCache != null)
            {
                _gatherableCache = otherGatherWorkTable._gatherableCache;
            }
            else
            {
                _gatherableCache = new GatherableCache(map);
            }
        }

        public IEnumerable<Thing> GetGatherableCandidates(RecipeDef_Gathering gatheringRecipeDef)
        {
            var buildingRegion = (def.hasInteractionCell ? InteractionCell : Position).GetRegion(Map);

            RegionEntryPredicate regionEntryPredicate = (Region from, Region r) =>
            {
                var extentsClose = r.extentsClose;
                int dx = Mathf.Abs(Position.x - Mathf.Max(extentsClose.minX, Mathf.Min(Position.x, extentsClose.maxX)));
                if (dx > gatherRadius) { return false; }

                int dz = Mathf.Abs(Position.z - Mathf.Max(extentsClose.minZ, Mathf.Min(Position.z, extentsClose.maxZ)));
                if (dz > gatherRadius) { return false; }

                return (dx * dx + dz * dz) <= gatherRadius * gatherRadius;
            };

            var gatherTargets = new HashSet<Thing>();
            RegionTraverser.BreadthFirstTraverse(buildingRegion, regionEntryPredicate, (r) =>
            {
                var gatherables = _gatherableCache.GetGatherablesCache(r, gatheringRecipeDef);
                gatherTargets.AddRange(gatherables);

                return false;

            }, maxRegions: 100);

            return gatherTargets.Where(v => this.InGatherableRange(v));
        }
    }
}
