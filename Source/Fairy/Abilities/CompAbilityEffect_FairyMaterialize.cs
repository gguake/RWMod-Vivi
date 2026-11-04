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

            // 준비 시간 중에 실체화할 수 없게 되면 아무 효과가 없으므로 재사용 대기를 돌리지 않는다.
            var ctrl = Controller;
            if (ctrl == null || !ctrl.TryQueueMaterializeAllAvailable())
            {
                parent.ResetCooldown();
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
