using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_DeliverPawnToEverflower : JobGiver_GotoTravelDestination
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var targetPawn = pawn.mindState.duty.focusSecond.Pawn;
            if (!pawn.CanReach(targetPawn.Position, PathEndMode.OnCell, PawnUtility.ResolveMaxDanger(pawn, maxDanger)))
            {
                return null;
            }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_DeliverToEverflower, targetPawn, pawn.mindState.duty.focus, pawn.mindState.duty.focusThird);
            job.locomotionUrgency = PawnUtility.ResolveLocomotion(pawn, locomotionUrgency);
            job.expiryInterval = jobMaxDuration;
            job.count = 1;
            return job;
        }
    }
}
