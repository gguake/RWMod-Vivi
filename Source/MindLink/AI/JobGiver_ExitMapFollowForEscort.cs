using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ExitMapFollowForEscort : JobGiver_ExitMapBest
    {
        public JobGiver_ExitMapFollowForEscort()
        {
            failIfCantJoinOrCreateCaravan = true;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(pawn))
            {
                return null;
            }

            return base.TryGiveJob(pawn);
        }
    }
}
