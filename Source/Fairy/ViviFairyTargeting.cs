using RimWorld;
using System.Collections;
using Verse;
using Verse.AI;

namespace VVRace
{
    public static class ViviFairyTargeting
    {
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
