using RimWorld;
using System.Collections;
using Verse;
using Verse.AI;

namespace VVRace
{
    public static class ViviFairyTargeting
    {
        public const float GuardScanRadius = 4.9f;
        public const float ConcentrationScanRadius = 28.9f;

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
                    var marker = p.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyConcentrated) as Hediff_FairyConcentrated;
                    if (marker == null || !marker.IsOwnedBy(owner)) { return false; }
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
                validator: (Thing t) =>
                {
                    if (t == exclude) { return false; }
                    if (!(t is IAttackTarget)) { return false; }
                    if (!t.Spawned || t.Fogged()) { return false; }
                    if (!t.HostileTo(faction)) { return false; }
                    if (excludeDowned && t is Pawn p && p.Downed) { return false; }
                    if (!GenSight.LineOfSightToThing(losFrom, t, map, skipFirstCell: true)) { return false; }
                    return true;
                },
                priorityGetter: (Thing t) =>
                    (t is IAttackTarget at && GenHostility.IsActiveThreatTo(at, faction) ? 10000f : 0f)
                    - center.DistanceToSquared(t.Position));
        }
    }
}
