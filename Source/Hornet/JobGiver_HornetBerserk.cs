using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_HornetBerserk : ThinkNode_JobGiver
    {
        private float maxAttackDistance = 90f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.TryGetAttackVerb(null) == null)
            {
                return null;
            }

            var fenceBlocked = pawn.def.race.FenceBlocked;
            var target = FindPawnTarget(pawn, fenceBlocked);
            if (target != null && pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, fenceBlocked))
            {
                return MeleeAttackJob(target, fenceBlocked);
            }

            var building = FindTurretTarget(pawn, fenceBlocked);
            if (building != null)
            {
                return MeleeAttackJob(building, fenceBlocked);
            }

            if (target != null)
            {
                using (var pawnPath = pawn.Map.pathFinder.FindPath(pawn.Position, target.Position, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors)))
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

        private Pawn FindPawnTarget(Pawn pawn, bool canBashFences)
        {
            return (Pawn)AttackTargetFinder.BestAttackTarget(
                pawn, 
                TargetScanFlags.NeedReachable,
                validator: thing => thing is Pawn pawn2 && pawn2.Spawned && !pawn2.Downed && !pawn2.IsInvisible(), 
                maxDist: maxAttackDistance, 
                canBashDoors: true,
                canBashFences: canBashFences);
        }

        private Building FindTurretTarget(Pawn pawn, bool canBashFences)
        {
            return (Building)AttackTargetFinder.BestAttackTarget(
                pawn, 
                TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedReachable | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, 
                validator: thing => thing is Building, 
                maxDist: maxAttackDistance,
                canBashFences: canBashFences);
        }
    }
}
