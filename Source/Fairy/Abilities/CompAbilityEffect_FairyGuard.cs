using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityFairyGuard : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityFairyGuard()
        {
            compClass = typeof(CompAbilityEffect_FairyGuard);
        }
    }

    public class CompAbilityEffect_FairyGuard : CompAbilityEffect
    {
        private const int RequiredFairies = 1;

        private CompViviHolder Controller => parent.pawn.GetComp<CompViviHolder>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            // 효과를 내지 못했으면 재사용 대기를 돌리지 않는다.
            if (!TryStartGuard(target.Pawn))
            {
                parent.ResetCooldown();
            }
        }

        private bool TryStartGuard(Pawn ally)
        {
            var ctrl = Controller;
            if (ally == null || ctrl == null) { return false; }

            // Valid 검사 후 준비 시간 중에 상태가 바뀔 수 있으므로 적용 직전에 다시 검증한다.
            if (ally.Dead || !ally.Spawned || ally.health == null) { return false; }
            if (ally.health.hediffSet.HasHediff(VVHediffDefOf.VV_FairyGuarded)) { return false; }

            if (!ctrl.TryReserveIdleFairies(RequiredFairies, out var reserved)) { return false; }

            int id = ctrl.NextFairyJobId();
            reserved[0].StartJob(new FairyJob_Guard(id, parent.pawn, ally));

            var hediff = (Hediff_FairyGuarded)ally.health.AddHediff(VVHediffDefOf.VV_FairyGuarded);
            hediff.ownerVivi = parent.pawn;
            hediff.jobId = id;
            return true;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            var ally = target.Pawn;
            if (ally == null || ally.Dead || !ally.Spawned || ally.health == null) { return false; }
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

}
