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
        private CompViviHolder Controller => parent.pawn.GetComp<CompViviHolder>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 준비 시간 중에 요정이 모두 사라졌거나 대상을 잃어 효과가 없으면 재사용 대기를 돌리지 않는다.
            var ctrl = Controller;
            var enemy = target.Pawn;
            if (ctrl == null || ctrl.MaterializedCount <= 0 || enemy == null || enemy.health == null)
            {
                parent.ResetCooldown();
                return;
            }

            Hediff_FairyConcentrated.RemoveOwnedBy(parent.pawn);
            var hediff = (Hediff_FairyConcentrated)enemy.health.AddHediff(VVHediffDefOf.VV_FairyConcentrated);
            hediff.ownerVivi = parent.pawn;
        }

        public override bool GizmoDisabled(out string reason)
        {
            var ctrl = Controller;
            if (ctrl == null || ctrl.MaterializedCount <= 0)
            {
                reason = LocalizeString_Etc.VV_AbilityDisabledNoMaterializedFairies.Translate();
                return true;
            }

            reason = null;
            return false;
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
            return base.Valid(target, throwMessages);
        }
    }
}
