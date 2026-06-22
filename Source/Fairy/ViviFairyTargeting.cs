using RimWorld;
using System.Collections;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public static class ViviFairyTargeting
    {
        public const float GuardScanRadius = 4.9f;
        public const float ConcentrationScanRadius = 36.9f;

        public static Thing FindConcentratedTarget(Pawn owner)
        {
            if (owner == null || !owner.Spawned || owner.Map == null) { return null; }

            var map = owner.Map;
            var faction = owner.Faction;
            IEnumerable potentialTargets = map.mapPawns.AllPawnsSpawned;

            return GenClosest.ClosestThing_Global(
                owner.Position,
                potentialTargets,
                maxDistance: ConcentrationScanRadius,
                validator: (Thing t) =>
                {
                    if (!(t is Pawn p)) { return false; }
                    if (!p.Spawned || p.Fogged() || p.DeadOrDowned || p.health == null) { return false; }
                    if (!p.HostileTo(owner)) { return false; }
                    if (Hediff_FairyConcentrated.GetOwnedBy(p, owner) == null) { return false; }
                    if (!GenSight.LineOfSightToThing(owner.Position, p, map, skipFirstCell: true)) { return false; }
                    return true;
                },
                priorityGetter: (Thing t) =>
                    (t is IAttackTarget at && GenHostility.IsActiveThreatTo(at, faction) ? 10000f : 0f)
                    - owner.Position.DistanceToSquared(t.Position));
        }

        public static Thing FindHostileNear(
            IAttackTargetSearcher searcher,
            IntVec3 center,
            float radius,
            IntVec3 losFrom,
            bool excludeDowned,
            Thing exclude = null)
        {
            var searcherThing = searcher != null ? searcher.Thing : null;
            var map = searcherThing != null ? searcherThing.Map : null;
            if (map == null) { return null; }

            var faction = searcherThing.Faction;
            IEnumerable potentialTargets = map.attackTargetsCache.GetPotentialTargetsFor(searcher);

            return GenClosest.ClosestThing_Global(
                center,
                potentialTargets,
                maxDistance: radius,
                validator: (Thing t) => IsValidHostileTarget(t, faction, map, losFrom, excludeDowned, exclude),
                priorityGetter: (Thing t) => (t is IAttackTarget at && GenHostility.IsActiveThreatTo(at, faction) ? 10000f : 0f) - Mathf.RoundToInt(center.DistanceToSquared(t.Position) / 5f));
        }

        public static Thing FindGuardTargetNear(
            IAttackTargetSearcher searcher,
            IntVec3 center,
            float radius,
            IntVec3 losFrom,
            IntVec3 priorityOrigin,
            bool excludeDowned,
            Thing exclude = null)
        {
            var searcherThing = searcher != null ? searcher.Thing : null;
            var map = searcherThing != null ? searcherThing.Map : null;
            if (map == null) { return null; }

            var faction = searcherThing.Faction;
            IEnumerable potentialTargets = map.attackTargetsCache.GetPotentialTargetsFor(searcher);
            Thing selected = null;
            int selectedDistance = int.MaxValue;
            int sameDistanceCount = 0;

            foreach (var potentialTarget in potentialTargets)
            {
                var target = potentialTarget as Thing;
                if (!IsValidHostileTarget(target, faction, map, losFrom, excludeDowned, exclude)) { continue; }
                if (center.DistanceTo(target.Position) > radius) { continue; }

                int distance = (int)priorityOrigin.DistanceTo(target.Position);
                if (distance < selectedDistance)
                {
                    selected = target;
                    selectedDistance = distance;
                    sameDistanceCount = 1;
                }
                else if (distance == selectedDistance)
                {
                    sameDistanceCount++;
                    if (Rand.Chance(1f / sameDistanceCount))
                    {
                        selected = target;
                    }
                }
            }

            return selected;
        }

        private static bool IsValidHostileTarget(Thing target, Faction faction, Map map, IntVec3 losFrom, bool excludeDowned, Thing exclude)
        {
            if (target == null || target == exclude) { return false; }
            if (!(target is IAttackTarget)) { return false; }
            if (!target.Spawned || target.Fogged()) { return false; }
            if (!target.HostileTo(faction)) { return false; }
            if (excludeDowned && target is Pawn p && p.Downed) { return false; }
            if (!GenSight.LineOfSightToThing(losFrom, target, map, skipFirstCell: true)) { return false; }
            return true;
        }
    }
}
