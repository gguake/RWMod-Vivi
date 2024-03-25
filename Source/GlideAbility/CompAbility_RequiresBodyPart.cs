using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityRequiresBodyPart : AbilityCompProperties
    {
        public BodyPartDef bodyPartDef;
        public int requiredCount;

        public CompProperties_AbilityRequiresBodyPart()
        {
            compClass = typeof(CompAbility_RequiresBodyPart);
        }
    }

    public class CompAbility_RequiresBodyPart : AbilityComp
    {
        public CompProperties_AbilityRequiresBodyPart Props => (CompProperties_AbilityRequiresBodyPart)props;

        public override bool GizmoDisabled(out string reason)
        {
            var bodyPartCount = parent.pawn.health.hediffSet.GetNotMissingParts().Count(v => v.def == Props.bodyPartDef);
            if (bodyPartCount < Props.requiredCount)
            {
                reason = LocalizeTexts.AbilityDisabledNoBodyPart.Translate(parent.pawn, Props.bodyPartDef.LabelCap, Props.requiredCount - bodyPartCount);
                return true;
            }

            reason = null;
            return false;
        }
    }
}
