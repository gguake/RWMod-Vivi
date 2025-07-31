using RimWorld;
using Verse;

namespace VVRace
{
    public class RitualRoleFairyViviBringer : RitualRoleEverflowerResonator
    {
        public override bool AppliesToPawn(Pawn pawn, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            if (!base.AppliesToPawn(pawn, out reason, selectedTarget, ritual, assignments, precept, skipReason))
            {
                return false;
            }

            var compViviHolder = pawn.GetComp<CompViviHolder>();
            if (compViviHolder == null)
            {
                return false;
            }

            if (compViviHolder.FairyficatedPawnCount == 0)
            {
                reason = LocalizeString_Etc.VV_FailReason_HasNoFairyVivi.Translate();
                return false;
            }

            return true;
        }
    }
}
