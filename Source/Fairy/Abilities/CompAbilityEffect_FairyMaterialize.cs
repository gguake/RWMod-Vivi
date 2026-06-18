using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityFairyMaterialize : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityFairyMaterialize()
        {
            compClass = typeof(CompAbilityEffect_FairyMaterialize);
        }
    }

    public class CompAbilityEffect_FairyMaterialize : CompAbilityEffect
    {
        private CompViviHolder Controller => parent.pawn.GetComp<CompViviHolder>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var ctrl = Controller;
            if (ctrl != null)
            {
                ctrl.TryQueueMaterializeAllAvailable();
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            var ctrl = Controller;
            if (ctrl == null || ctrl.FairyficatedPawnCount <= ctrl.MaterializedCount + ctrl.PendingMaterializeCount)
            {
                reason = LocalizeString_Etc.VV_AbilityDisabledNoFairySlot.Translate();
                return true;
            }

            reason = null;
            return false;
        }
    }
}
