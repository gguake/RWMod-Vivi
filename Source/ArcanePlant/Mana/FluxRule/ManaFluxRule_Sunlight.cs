using Verse;

namespace VVRace
{
    public class ManaFluxRule_Sunlight : ManaFluxRule
    {
        public IntRange manaFromSunlightRange;

        public override IntRange FluxRangeForDisplay => new IntRange(manaFromSunlightRange.min, manaFromSunlightRange.max);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Sunlight_Desc.Translate(
                manaFromSunlightRange.TrueMax.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            if (thing.Map.roofGrid.Roofed(thing.Position))
            {
                return 0;
            }

            return manaFromSunlightRange.Lerped(thing.Map.skyManager.CurSkyGlow);
        }
    }
}
