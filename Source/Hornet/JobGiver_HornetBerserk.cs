using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class JobGiver_HornetBerserk : ThinkNode_JobGiver
    {
        private const float DefaultMaxTargetSearchDistance = 90f;

        // 즉시 교전 검사가 만든 작업을 같은 틱의 작업 재선택에서 다시 탐색하지 않고 지급한다.
        private static Pawn _preparedPawn;
        private static Job _preparedJob;
        private static int _preparedTick = -1;

        private float maxTargetSearchDistance = DefaultMaxTargetSearchDistance;

        public static bool TryPrepareAttackJob(Pawn pawn)
        {
            var job = MakeAttackJob(pawn, DefaultMaxTargetSearchDistance);
            if (job == null) { return false; }

            _preparedPawn = pawn;
            _preparedJob = job;
            _preparedTick = Find.TickManager.TicksGame;
            return true;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (_preparedPawn == pawn && _preparedTick == Find.TickManager.TicksGame && maxTargetSearchDistance == DefaultMaxTargetSearchDistance)
            {
                var job = _preparedJob;
                _preparedPawn = null;
                _preparedJob = null;
                return job;
            }

            return MakeAttackJob(pawn, maxTargetSearchDistance);
        }

        private static Job MakeAttackJob(Pawn pawn, float maxDist)
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

            var target = FindPawnTarget(pawn, maxDist, fenceBlocked);
            if (target != null && pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, fenceBlocked))
            {
                return MeleeAttackJob(target, fenceBlocked);
            }

            var building = FindTurretTarget(pawn, maxDist, fenceBlocked);
            if (building != null)
            {
                return MeleeAttackJob(building, fenceBlocked);
            }

            if (target != null)
            {
                return ApproachBlockedTargetJob(pawn, target);
            }
            return null;
        }

        // 문에 막혀 닿지 못하는 대상은 막는 문 앞까지 다가가 문을 부술 기회를 만든다.
        internal static Job ApproachBlockedTargetJob(Pawn pawn, Thing target)
        {
            using (var pawnPath = pawn.Map.pathFinder.FindPathNow(pawn.Position, target.Position, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors)))
            {
                if (!pawnPath.Found)
                {
                    return null;
                }

                if (!pawnPath.TryFindLastCellBeforeBlockingDoor(pawn, out var result))
                {
                    // 대상이 문 칸에 서 있으면 경로 끝 칸이 그 문이라 막는 문으로 검색되지 않는다(바닐라 JobGiver_Manhunter와 같은 처리).
                    if (target.Position.GetDoor(target.Map) == null)
                    {
                        Log.Error(string.Concat(pawn, " did TryFindLastCellBeforeDoor but found none when it should have been one. Target: ", target.LabelCap));
                        return null;
                    }

                    result = pawnPath.NodesReversed[1];
                }

                var randomCell = CellFinder.RandomRegionNear(result.GetRegion(pawn.Map), 9, TraverseParms.For(pawn)).RandomCell;
                if (randomCell == pawn.Position)
                {
                    return JobMaker.MakeJob(JobDefOf.Wait, 500);
                }

                return JobMaker.MakeJob(JobDefOf.Goto, randomCell);
            }
        }

        private static Job MeleeAttackJob(Thing target, bool canBashFences)
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
