using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class HediffCompProperties_AgingFactor : HediffCompProperties
    {
        public float factor = 1f;

        public HediffCompProperties_AgingFactor()
        {
            compClass = typeof(HediffComp_AgingFactor);
        }
    }

    public class HediffComp_AgingFactor : HediffComp
    {
        public HediffCompProperties_AgingFactor Props => (HediffCompProperties_AgingFactor)props;

        public override string CompTipStringExtra
            => LocalizeString_Etc.VV_Hediff_AgingFactor.Translate(Props.factor.ToStringPercentEmptyZero());

        public static float ApplyAgingFactor(float factor, Pawn_GeneTracker geneTracker)
        {
            foreach (var hediffComp in geneTracker?.pawn?.health?.hediffSet.GetHediffComps<HediffComp_AgingFactor>())
            {
                factor *= hediffComp.Props.factor;
            }

            return factor;
        }
    }
}
