using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_HornetBerserk : ThinkNode_JobGiver
    {
        private float maxTargetSearchDistance = 70f;

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
                validator: thing => thing is Pawn targetPawn && targetPawn.Spawned && !targetPawn.Downed && !targetPawn.IsPsychologicallyInvisible(),
                maxDist: maxTargetSearchDistance, 
                canBashDoors: true,
                canBashFences: canBashFences);
        }

        private Building FindTurretTarget(Pawn pawn, bool canBashFences)
        {
            return (Building)AttackTargetFinder.BestAttackTarget(
                pawn, 
                TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedReachable | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, 
                validator: thing => thing is Building, 
                maxDist: maxTargetSearchDistance,
                canBashFences: canBashFences);
        }
    }
}
