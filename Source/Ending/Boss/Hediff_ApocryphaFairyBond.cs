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
                var comp = pawn.TryGetComp<CompApocrypha>();
                return comp == null ? base.TipStringExtra : comp.BondLabel;
            }
        }
    }
}
