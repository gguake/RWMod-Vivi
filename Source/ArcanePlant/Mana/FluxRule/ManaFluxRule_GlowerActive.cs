using Verse;

namespace VVRace
{
    public class ManaFluxRule_GlowerActive : ManaFluxRule
    {
        public int mana;

        public override IntRange FluxRangeForDisplay => new IntRange(0, mana);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_GlowerActive_Desc.Translate(mana.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            var compGlower = thing.TryGetComp<CompManaGlower>();
            if (compGlower.Glows)
            {
                return mana;
            }

            return 0;
        }
    }
}
