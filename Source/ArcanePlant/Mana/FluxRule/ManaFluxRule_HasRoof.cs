using Verse;

namespace VVRace
{
    public class ManaFluxRule_HasRoof : ManaFluxRule
    {
        public int mana;

        public override IntRange FluxRangeForDisplay => new IntRange(0, mana);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_HasRoof_Desc.Translate(mana.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            if (thing.Position.Roofed(thing.Map))
            {
                return mana;
            }

            return 0;
        }
    }
}
