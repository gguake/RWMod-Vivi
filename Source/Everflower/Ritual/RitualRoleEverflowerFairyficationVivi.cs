using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class RitualRoleEverflowerFairyficationVivi : RitualRoleColonist
    {
        public override bool AppliesToPawn(
            Pawn pawn, 
            out string reason, 
            TargetInfo selectedTarget, 
            LordJob_Ritual ritual = null, 
            RitualRoleAssignments assignments = null, 
            Precept_Ritual precept = null, 
            bool skipReason = false)
        {
            if (!base.AppliesToPawn(pawn, out reason, selectedTarget, ritual, assignments, precept, skipReason)) { return false; }

            if (!selectedTarget.HasThing) { return false; }

            var everflower = selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return false; }

            if (!pawn.TryGetComp<CompVivi>(out var compVivi))
            {
                reason = LocalizeString_Etc.VV_FailReason_NotVivi.Translate();
                return false;
            }

            if (compVivi.isRoyal)
            {
                reason = LocalizeString_Etc.VV_FailReason_RoyalVivi.Translate();
                return false;
            }

            if (assignments != null)
            {
                var resonator = assignments.AssignedPawns("resonator").FirstOrDefault();
                if (resonator != null && pawn.GetMother() != resonator)
                {
                    reason = LocalizeString_Etc.VV_FailReason_NotMyChild.Translate();
                    return false;
                }
            }

            if (pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_FairyficationSickness))
            {
                reason = LocalizeString_Etc.VV_FailReason_FairyficationSickness.Translate();
                return false;
            }

            return true;
        }

        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn pawn = null, bool skipReason = false)
        {
            reason = null;
            return false;
        }
    }
}
