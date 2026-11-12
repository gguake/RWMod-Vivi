using Verse;

namespace VVRace
{
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
