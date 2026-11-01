using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class JobGiver_HornetBerserk : ThinkNode_JobGiver
    {
        private const float DefaultMaxTargetSearchDistance = 90f;

        private float maxTargetSearchDistance = DefaultMaxTargetSearchDistance;

        public static bool HasAttackTarget(Pawn pawn)
        {
            if (pawn.TryGetAttackVerb(null) == null)
            {
                return false;
            }

            var fenceBlocked = pawn.def.race.FenceBlocked;
            return GetReachableFocusTarget(pawn, fenceBlocked) != null
                || FindPawnTarget(pawn, DefaultMaxTargetSearchDistance, fenceBlocked) != null
                || FindTurretTarget(pawn, DefaultMaxTargetSearchDistance, fenceBlocked) != null;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.TryGetAttackVerb(null) == null)
            {
                return null;
            }

            var fenceBlocked = pawn.def.race.FenceBlocked;

            var focus = GetReachableFocusTarget(pawn, fenceBlocked);
            if (focus != null)
            {
                return MeleeAttackJob(focus, fenceBlocked);
            }

            var target = FindPawnTarget(pawn, maxTargetSearchDistance, fenceBlocked);
            if (target != null && pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, fenceBlocked))
            {
                return MeleeAttackJob(target, fenceBlocked);
            }

            var building = FindTurretTarget(pawn, maxTargetSearchDistance, fenceBlocked);
            if (building != null)
            {
                return MeleeAttackJob(building, fenceBlocked);
            }

            if (target != null)
            {
                using (var pawnPath = pawn.Map.pathFinder.FindPathNow(pawn.Position, target.Position, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors)))
                {
                    if (!pawnPath.Found)
                    {
                        return null;
                    }

                    if (!pawnPath.TryFindLastCellBeforeBlockingDoor(pawn, out var result))
                    {
                        Log.Error(string.Concat(pawn, " did TryFindLastCellBeforeDoor but found none when it should have been one. Target: ", target.LabelCap));
                        return null;
                    }

                    var randomCell = CellFinder.RandomRegionNear(result.GetRegion(pawn.Map), 9, TraverseParms.For(pawn)).RandomCell;
                    if (randomCell == pawn.Position)
                    {
                        return JobMaker.MakeJob(JobDefOf.Wait, 500);
                    }

                    return JobMaker.MakeJob(JobDefOf.Goto, randomCell);
                }
            }
            return null;
        }

        private Job MeleeAttackJob(Thing target, bool canBashFences)
        {
            var job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            job.maxNumMeleeAttacks = 1;
            job.expiryInterval = Rand.Range(420, 900);
            job.attackDoorIfTargetLost = true;
            job.canBashFences = canBashFences;
            return job;
        }

        private static Pawn FindPawnTarget(Pawn pawn, float maxDist, bool canBashFences)
        {
            GetSearchArea(pawn, out var locus, out var maxTravelRadius);
            return (Pawn)AttackTargetFinder.BestAttackTarget(
                pawn,
                TargetScanFlags.NeedReachable,
                validator: thing => thing is Pawn targetPawn && targetPawn.Spawned && !targetPawn.Downed && !targetPawn.IsPsychologicallyInvisible(),
                maxDist: maxDist,
                locus: locus,
                maxTravelRadiusFromLocus: maxTravelRadius,
                canBashDoors: true,
                canBashFences: canBashFences);
        }

        private static Building FindTurretTarget(Pawn pawn, float maxDist, bool canBashFences)
        {
            GetSearchArea(pawn, out var locus, out var maxTravelRadius);
            return (Building)AttackTargetFinder.BestAttackTarget(
                pawn,
                TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedReachable | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                validator: thing => thing is Building,
                maxDist: maxDist,
                locus: locus,
                maxTravelRadiusFromLocus: maxTravelRadius,
                canBashFences: canBashFences);
        }

        private static Thing GetReachableFocusTarget(Pawn pawn, bool fenceBlocked)
        {
            if (!(pawn.GetLord()?.LordJob is LordJob_ApocryphaEscort escortJob)) { return null; }

            var focus = escortJob.FocusTarget;
            if (focus == null || focus.Map != pawn.Map) { return null; }

            return pawn.CanReach(focus, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, fenceBlocked) ? focus : null;
        }

        private static void GetSearchArea(Pawn pawn, out IntVec3 locus, out float maxTravelRadius)
        {
            if (pawn.GetLord()?.LordJob is LordJob_ApocryphaHornetGuard guardJob && guardJob.BossActive && guardJob.Boss.Map == pawn.Map)
            {
                locus = guardJob.GetGuardPoint(pawn);
                maxTravelRadius = guardJob.SearchRadius;
                return;
            }

            locus = default;
            maxTravelRadius = float.MaxValue;
        }
    }
}
