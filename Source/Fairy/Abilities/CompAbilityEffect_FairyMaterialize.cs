using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityFairyMaterialize : CompProperties_AbilityEffect
    {
        // 실체화 시 대상 셀에서 소모하는 환경 마나량. 해당 셀의 마나가 이보다 적으면 시전 불가.
        public float manaCost = 100f;

        public CompProperties_AbilityFairyMaterialize()
        {
            compClass = typeof(CompAbilityEffect_FairyMaterialize);
        }
    }

    public class CompAbilityEffect_FairyMaterialize : CompAbilityEffect
    {
        public new CompProperties_AbilityFairyMaterialize Props => (CompProperties_AbilityFairyMaterialize)props;

        private CompViviFairyController Controller => parent.pawn.GetComp<CompViviFairyController>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var pawn = parent.pawn;
            var map = pawn.Map;
            if (map == null) { return; }

            var ctrl = Controller;
            if (ctrl == null || !ctrl.CanMaterialize) { return; }

            var cell = target.Cell;
            var manaComp = map.GetManaComponent();
            if (manaComp == null || manaComp[cell] < Props.manaCost) { return; }

            manaComp.ChangeEnvironmentMana(cell, -Props.manaCost);
            ctrl.MaterializeFairyAt(cell);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            var pawn = parent.pawn;
            var map = pawn.Map;
            if (map == null || !target.IsValid || !target.Cell.InBounds(map))
            {
                return false;
            }

            var manaComp = map.GetManaComponent();
            if (manaComp == null || manaComp[target.Cell] < Props.manaCost)
            {
                if (throwMessages)
                {
                    Messages.Message(LocalizeString_Etc.VV_AbilityFailNoMana.Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override bool GizmoDisabled(out string reason)
        {
            var ctrl = Controller;
            if (ctrl == null || !ctrl.CanMaterialize)
            {
                reason = LocalizeString_Etc.VV_AbilityDisabledNoFairySlot.Translate();
                return true;
            }

            reason = null;
            return false;
        }
    }
}
