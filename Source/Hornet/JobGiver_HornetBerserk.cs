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

        // 이 작업 지급기가 지금 공격(또는 접근) 작업을 줄 대상이 있는지. 호위 로드가 대기 중인 말벌을 바로 교전시킬 때 쓴다.
        public static bool HasAttackTarget(Pawn pawn)
        {
            if (pawn.TryGetAttackVerb(null) == null)
            {
                return false;
            }

            var fenceBlocked = pawn.def.race.FenceBlocked;
            return FindPawnTarget(pawn, DefaultMaxTargetSearchDistance, fenceBlocked) != null
                || FindTurretTarget(pawn, DefaultMaxTargetSearchDistance, fenceBlocked) != null;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.TryGetAttackVerb(null) == null)
            {
                return null;
            }

            var fenceBlocked = pawn.def.race.FenceBlocked;
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

        // 아포크리파 호위 말벌은 보스 주변만 탐색해 보스 곁을 지킨다(경호/반격 대상 선별은 LordJob_ApocryphaEscort.ValidateAttackTarget). 그 외 말벌은 제한 없음.
        private static void GetSearchArea(Pawn pawn, out IntVec3 locus, out float maxTravelRadius)
        {
            if (pawn.GetLord()?.LordJob is LordJob_ApocryphaEscort escortJob && escortJob.BossActive && escortJob.Boss.Map == pawn.Map)
            {
                locus = escortJob.Boss.Position;
                maxTravelRadius = escortJob.SearchRadius;
                return;
            }

            locus = default;
            maxTravelRadius = float.MaxValue;
        }
    }
}
