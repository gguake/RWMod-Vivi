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

            if (Hediff_FairyConcentrated.ActiveMarkers.Count == 0) { return null; }

            var map = owner.Map;
            var faction = owner.Faction;
            var ownerPos = owner.Position;
            var pawns = map.mapPawns.AllPawnsSpawned;
            float maxDistSq = ConcentrationScanRadius * ConcentrationScanRadius;

            Pawn best = null;
            float bestPriority = float.MinValue;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                if (p == null || !p.Spawned || p.Fogged() || p.DeadOrDowned || p.health == null) { continue; }

                int distSq = ownerPos.DistanceToSquared(p.Position);
                if (distSq > maxDistSq) { continue; }
                if (!p.HostileTo(owner)) { continue; }
                if (Hediff_FairyConcentrated.GetOwnedBy(p, owner) == null) { continue; }
                if (!GenSight.LineOfSightToThing(ownerPos, p, map, skipFirstCell: true)) { continue; }

                float priority = (GenHostility.IsActiveThreatTo(p, faction) ? 10000f : 0f) - distSq;
                if (best != null && (priority < bestPriority || (Mathf.Approximately(priority, bestPriority) && distSq >= bestDistSq)))
                {
                    continue;
                }

                best = p;
                bestPriority = priority;
                bestDistSq = distSq;
            }

            return best;
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
            var potentialTargets = map.attackTargetsCache.GetPotentialTargetsFor(searcher);
            if (potentialTargets == null || potentialTargets.Count == 0) { return null; }

            float radiusSq = radius * radius;
            Thing best = null;
            float bestPriority = float.MinValue;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < potentialTargets.Count; i++)
            {
                var at = potentialTargets[i];
                var target = at as Thing;
                if (target == null) { continue; }

                int distSq = center.DistanceToSquared(target.Position);
                if (distSq > radiusSq) { continue; }
                if (!IsValidHostileTarget(target, faction, map, losFrom, excludeDowned, exclude)) { continue; }

                float priority = (GenHostility.IsActiveThreatTo(at, faction) ? 10000f : 0f) - Mathf.RoundToInt(distSq / 5f);
                if (best != null && (priority < bestPriority || (Mathf.Approximately(priority, bestPriority) && distSq >= bestDistSq)))
                {
                    continue;
                }

                best = target;
                bestPriority = priority;
                bestDistSq = distSq;
            }

            return best;
        }

        public static Thing FindGuardTargetNear(
            IAttackTargetSearcher searcher,
            IntVec3 center,
            float radius,
            IntVec3 losFrom,
            IntVec3 priorityOrigin,
            bool excludeDowned,
            Thing exclude = null,
            bool pawnsOnly = false)
        {
            var searcherThing = searcher != null ? searcher.Thing : null;
            var map = searcherThing != null ? searcherThing.Map : null;
            if (map == null) { return null; }

            var faction = searcherThing.Faction;
            var potentialTargets = map.attackTargetsCache.GetPotentialTargetsFor(searcher);
            if (potentialTargets == null || potentialTargets.Count == 0) { return null; }

            Thing selected = null;
            int selectedDistance = int.MaxValue;
            int sameDistanceCount = 0;

            for (int i = 0; i < potentialTargets.Count; i++)
            {
                var target = potentialTargets[i] as Thing;
                if (target == null) { continue; }
                if (pawnsOnly && !(target is Pawn)) { continue; }
                if (center.DistanceTo(target.Position) > radius) { continue; }
                if (!IsValidHostileTarget(target, faction, map, losFrom, excludeDowned, exclude)) { continue; }

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
