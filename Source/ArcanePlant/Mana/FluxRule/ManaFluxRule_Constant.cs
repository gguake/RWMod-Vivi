using Verse;

namespace VVRace
{
    public class ManaFluxRule_Constant : ManaFluxRule
    {
        public int mana;

        public override IntRange FluxRangeForDisplay => new IntRange(mana, mana);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Constant_Desc.Translate(mana.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            return mana;
        }
    }
}
