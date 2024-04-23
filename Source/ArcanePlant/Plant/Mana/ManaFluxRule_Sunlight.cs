using Verse;

namespace VVRace
{
    public class ManaFluxRule_Sunlight : ManaFluxRule
    {
        public IntRange manaFromSunlightRange;

        public override IntRange ApproximateManaFlux => new IntRange(manaFromSunlightRange.min, manaFromSunlightRange.max);

        public override string GetRuleString(bool inverse) =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Sunlight_Desc.Translate(
                inverse ? -manaFromSunlightRange.TrueMax : manaFromSunlightRange.TrueMax);

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0; }

            if (plant.Map.roofGrid.Roofed(plant.Position))
            {
                return 0;
            }

            return manaFromSunlightRange.Lerped(plant.Map.skyManager.CurSkyGlow);
        }
    }
}
