using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_AIFollowForEscort : JobGiver_AIFollowPawn
    {
        protected override int FollowJobExpireInterval => 200;

        protected override Pawn GetFollowee(Pawn pawn)
        {
            return pawn.GetMindLinkMasterWithoutCheck();
        }

        protected override float GetRadius(Pawn pawn)
        {
            return 5f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.GetMindLinkMaster() == null)
            {
                return null;
            }

            return base.TryGiveJob(pawn);
        }
    }
}
