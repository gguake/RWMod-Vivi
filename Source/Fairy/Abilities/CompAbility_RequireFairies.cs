using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityRequireFairies : AbilityCompProperties
    {
        public int requiredIdle = 1;

        public CompProperties_AbilityRequireFairies()
        {
            compClass = typeof(CompAbility_RequireFairies);
        }
    }

    public class CompAbility_RequireFairies : AbilityComp
    {
        public CompProperties_AbilityRequireFairies Props => (CompProperties_AbilityRequireFairies)props;

        private CompViviHolder Controller => parent.pawn.GetComp<CompViviHolder>();

        public override bool GizmoDisabled(out string reason)
        {
            var ctrl = Controller;
            if (ctrl == null || ctrl.AvailableCount < Props.requiredIdle)
            {
                reason = LocalizeString_Etc.VV_AbilityDisabledNotEnoughFairies.Translate(Props.requiredIdle);
                return true;
            }

            reason = null;
            return false;
        }
    }
}
