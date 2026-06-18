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
        private const int RequiredFairies = 3;

        private CompViviFairyController Controller => parent.pawn.GetComp<CompViviFairyController>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var enemy = target.Pawn;
            var ctrl = Controller;
            if (enemy == null || ctrl == null) { return; }
            if (!ctrl.TryReserveIdleFairies(RequiredFairies, out var reserved)) { return; }

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

}
