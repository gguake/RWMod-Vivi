using RimWorld;
using Verse;

namespace VVRace
{
    // 사용 가능한 요정 수가 부족하면 능력을 비활성화하는 공용 게이트.
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

        private CompViviFairyController Controller => parent.pawn.GetComp<CompViviFairyController>();

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

    // 요정 - 경호
    public class CompProperties_AbilityFairyGuard : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityFairyGuard()
        {
            compClass = typeof(CompAbilityEffect_FairyGuard);
        }
    }

    public class CompAbilityEffect_FairyGuard : CompAbilityEffect
    {
        private CompViviFairyController Controller => parent.pawn.GetComp<CompViviFairyController>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var ally = target.Pawn;
            var ctrl = Controller;
            if (ally == null || ctrl == null) { return; }
            if (!ctrl.TryReserveIdleFairies(1, FairyRole.Guard, out var reserved)) { return; }

            int id = ctrl.NextJobId();
            reserved[0].StartJob(new FairyJob_Guard(id, parent.pawn, ally));

            var hediff = (Hediff_FairyGuarded)ally.health.AddHediff(VVHediffDefOf.VV_FairyGuarded);
            hediff.ownerVivi = parent.pawn;
            hediff.sessionId = id;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            var ally = target.Pawn;
            if (ally == null || ally.Dead || !ally.Spawned) { return false; }
            if (ally == parent.pawn) { return false; }

            if (ally.HostileTo(parent.pawn))
            {
                if (throwMessages) { Messages.Message(LocalizeString_Etc.VV_AbilityFailNotAlly.Translate(), ally, MessageTypeDefOf.RejectInput, historical: false); }
                return false;
            }
            if (ally.health.hediffSet.HasHediff(VVHediffDefOf.VV_FairyGuarded))
            {
                if (throwMessages) { Messages.Message(LocalizeString_Etc.VV_AbilityFailAlreadyGuarded.Translate(), ally, MessageTypeDefOf.RejectInput, historical: false); }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }

    // 요정 - 집중
    public class CompProperties_AbilityFairyConcentration : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityFairyConcentration()
        {
            compClass = typeof(CompAbilityEffect_FairyConcentration);
        }
    }

    public class CompAbilityEffect_FairyConcentration : CompAbilityEffect
    {
        private CompViviFairyController Controller => parent.pawn.GetComp<CompViviFairyController>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var enemy = target.Pawn;
            var ctrl = Controller;
            if (enemy == null || ctrl == null) { return; }
            if (!ctrl.TryReserveIdleFairies(3, FairyRole.Concentration, out var reserved)) { return; }

            int id = ctrl.NextJobId();
            for (int i = 0; i < reserved.Count; i++)
            {
                reserved[i].StartJob(new FairyJob_Concentration(id, parent.pawn, enemy, i, reserved.Count));
            }

            enemy.health.AddHediff(VVHediffDefOf.VV_FairyConcentrated);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            var enemy = target.Pawn;
            if (enemy == null || enemy.Dead || !enemy.Spawned) { return false; }

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

    // 요정 - 전개 (자기 시전 토글)
    public class CompProperties_AbilityFairyExpansion : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityFairyExpansion()
        {
            compClass = typeof(CompAbilityEffect_FairyExpansion);
        }
    }

    public class CompAbilityEffect_FairyExpansion : CompAbilityEffect
    {
        private const int RequiredFairies = 6;

        private CompViviFairyController Controller => parent.pawn.GetComp<CompViviFairyController>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var ctrl = Controller;
            if (ctrl == null) { return; }

            var active = ctrl.GetActiveJob<FairyJob_Expansion>();
            if (active != null)
            {
                ctrl.InterruptJob(active.id, FairyJobInterruptReason.AbilityToggledOff);
                return;
            }

            if (!ctrl.TryReserveIdleFairies(RequiredFairies, FairyRole.Expansion, out var reserved)) { return; }

            int id = ctrl.NextJobId();
            for (int i = 0; i < reserved.Count; i++)
            {
                reserved[i].StartJob(new FairyJob_Expansion(id, parent.pawn, i, reserved.Count));
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            var ctrl = Controller;
            // 이미 전개 중이면 토글 해제 가능하므로 활성.
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
