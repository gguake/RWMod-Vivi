using Verse;

namespace VVRace
{
    public class Hediff_ApocryphaStuckNeedle : HediffWithComps
    {
        public override string SeverityLabel => null;

        public override bool Visible => StuckCount > 0;

        public override bool ShouldRemove => StuckCount <= 0 || base.ShouldRemove;

        private int StuckCount
        {
            get
            {
                var comp = pawn?.TryGetComp<CompApocrypha>();
                return comp != null && comp.Phase == ApocryphaPhase.Queen ? comp.NeedleAttack.StuckNeedleCount : 0;
            }
        }

        public override string TipStringExtra
        {
            get
            {
                var comp = pawn?.TryGetComp<CompApocrypha>();
                var tip = base.TipStringExtra;
                if (comp == null) { return tip; }

                var needles = LocalizeString_Etc.VV_Hediff_ApocryphaStuckNeedleTip.Translate(
                    comp.NeedleAttack.StuckNeedleCount, comp.Props.queenNeedleCount, comp.IncomingDamageFactor.ToStringPercent());
                return EndingVfxUtility.AppendTipLine(tip, needles);
            }
        }
    }
}
