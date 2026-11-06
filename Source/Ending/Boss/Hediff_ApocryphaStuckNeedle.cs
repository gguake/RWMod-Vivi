using Verse;

namespace VVRace
{
    public class Hediff_ApocryphaStuckNeedle : HediffWithComps
    {
        public override string SeverityLabel => null;

        public override string TipStringExtra
        {
            get
            {
                var comp = pawn?.TryGetComp<CompApocrypha>();
                var tip = base.TipStringExtra;
                if (comp == null) { return tip; }

                var needles = LocalizeString_Etc.VV_Hediff_ApocryphaStuckNeedleTip.Translate(
                    comp.NeedleAttack.StuckNeedleCount, comp.Props.unboundNeedleCount, comp.IncomingDamageFactor.ToStringPercent());
                return tip.NullOrEmpty() ? (string)needles : tip + "\n" + needles;
            }
        }
    }
}
