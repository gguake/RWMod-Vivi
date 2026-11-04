using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityFairyExpansion : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityFairyExpansion()
        {
            compClass = typeof(CompAbilityEffect_FairyExpansion);
        }
    }

    public class CompAbilityEffect_FairyExpansion : CompAbilityEffect
    {
        private const int RequiredFairies = 3;

        private CompViviHolder Controller => parent.pawn.GetComp<CompViviHolder>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var ctrl = Controller;
            if (ctrl == null)
            {
                parent.ResetCooldown();
                return;
            }

            var active = ctrl.GetActiveJob<FairyJob_Expansion>();
            if (active != null)
            {
                ctrl.InterruptJob(active.id, FairyJobInterruptReason.AbilityToggledOff);
                return;
            }

            var reserved = ctrl.ActiveFairies
                .Where(f => f != null && !f.Destroyed && f.IsAvailable)
                .OrderBy(f => f.thingIDNumber)
                .ToList();
            // 준비 시간 중에 요정이 다른 일을 맡아 부족해지면 아무 효과가 없으므로 재사용 대기를 돌리지 않는다.
            if (reserved.Count < RequiredFairies)
            {
                parent.ResetCooldown();
                return;
            }

            int id = ctrl.NextFairyJobId();
            for (int i = 0; i < reserved.Count; i++)
            {
                reserved[i].StartJob(new FairyJob_Expansion(id, parent.pawn, i, reserved.Count));
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            var ctrl = Controller;
            // 전개 중에도 해제할 수 있도록 활성 상태를 유지한다.
            if (ctrl != null && ctrl.GetActiveJob<FairyJob_Expansion>() != null)
            {
                reason = null;
                return false;
            }

            if (ctrl == null || ctrl.AvailableCount < RequiredFairies)
            {
                reason = LocalizeString_Etc.VV_AbilityDisabledNotEnoughFairies.Translate(RequiredFairies);
                return true;
            }

            reason = null;
            return false;
        }
    }
}
