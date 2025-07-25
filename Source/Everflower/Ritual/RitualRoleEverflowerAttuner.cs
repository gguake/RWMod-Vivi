using RimWorld;
using Verse;

namespace VVRace
{
    public class RitualRoleEverflowerAttuner : RitualRoleColonist
    {
        public override bool AppliesToPawn(Pawn pawn, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            if (!base.AppliesToPawn(pawn, out reason, selectedTarget, ritual, assignments, precept, skipReason)) { return false; }

            if (!selectedTarget.HasThing) { return false; }

            var everflower = selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return false; }

            if (!pawn.TryGetComp<CompVivi>(out var compVivi) || !compVivi.isRoyal)
            {
                reason = LocalizeString_Etc.VV_FailReason_NotRoyalVivi.Translate();
                return false;
            }

            if (compVivi.LinkedEverflower != null && compVivi.LinkedEverflower != everflower)
            {
                reason = LocalizeString_Etc.VV_FailReason_AlreadyAttunedOther.Translate();
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
