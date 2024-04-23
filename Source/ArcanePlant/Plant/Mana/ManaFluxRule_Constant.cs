using Verse;

namespace VVRace
{
    public class ManaFluxRule_Constant : ManaFluxRule
    {
        public int mana;

        public override IntRange ApproximateManaFlux => new IntRange(mana, mana);

        public override string GetRuleString(bool inverse) =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Constant_Desc.Translate(
            (inverse ? -mana : mana).ToString("+0;-#"));

        public override int CalcManaFlux(ManaAcceptor manaAcceptor)
        {
            return mana;
        }
    }
}
