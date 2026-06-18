using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityFairyConcentration : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityFairyConcentration()
        {
            compClass = typeof(CompAbilityEffect_FairyConcentration);
        }
    }

    public class CompAbilityEffect_FairyConcentration : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var enemy = target.Pawn;
            if (enemy == null || enemy.health == null) { return; }

            var hediff = (Hediff_FairyConcentrated)enemy.health.AddHediff(VVHediffDefOf.VV_FairyConcentrated);
            hediff.ownerVivi = parent.pawn;
            hediff.ResetDuration();
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            var enemy = target.Pawn;
            if (enemy == null || enemy.DeadOrDowned || !enemy.Spawned || enemy.health == null) { return false; }

            if (!enemy.HostileTo(parent.pawn))
            {
                if (throwMessages) { Messages.Message(LocalizeString_Etc.VV_AbilityFailNotHostile.Translate(), enemy, MessageTypeDefOf.RejectInput, historical: false); }
                return false;
            }
            if (enemy.health.hediffSet.HasHediff(VVHediffDefOf.VV_FairyConcentrated))
            {
                if (throwMessages) { Messages.Message(LocalizeString_Etc.VV_AbilityFailAlreadyConcentrated.Translate(), enemy, MessageTypeDefOf.RejectInput, historical: false); }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }
}
