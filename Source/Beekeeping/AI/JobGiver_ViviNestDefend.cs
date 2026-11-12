using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestDefend : JobGiver_AIDefendPoint
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.GetNestLordJob() == null || pawn.equipment?.Primary == null || pawn.WorkTagIsDisabled(WorkTags.Violent)) { return null; }

            return base.TryGiveJob(pawn);
        }
    }
}
