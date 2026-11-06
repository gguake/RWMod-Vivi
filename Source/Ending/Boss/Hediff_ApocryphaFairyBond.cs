using Verse;

namespace VVRace
{
    // 14억 이상의 float 값에는 1 감소가 반영되지 않으므로 severity는 1로 고정하고 실제 결속 수는 툴팁에만 표시한다.
    public class Hediff_ApocryphaFairyBond : HediffWithComps
    {
        public override string SeverityLabel => null;

        public override string TipStringExtra
        {
            get
            {
                var comp = pawn?.TryGetComp<CompApocrypha>();
                if (comp == null) { return base.TipStringExtra; }

                return LocalizeString_Etc.VV_Hediff_ApocryphaFairyBondTip.Translate(
                    comp.BondCount.ToString("N0"),
                    comp.InitialBondCount.ToString("N0"));
            }
        }
    }
}
