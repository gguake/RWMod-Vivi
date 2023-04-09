using RimWorld;
using Verse;

namespace VVRace
{
    public class JobGiver_AIDefendForEscort : JobGiver_AIDefendPawn
    {
        protected override Pawn GetDefendee(Pawn pawn)
        {
            return pawn.GetMindLinkMaster();
        }

        protected override float GetFlagRadius(Pawn pawn)
        {
            return 5f;
        }
    }
}
