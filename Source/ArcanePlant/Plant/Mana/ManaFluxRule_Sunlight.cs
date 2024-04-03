using Verse;

namespace VVRace
{
    public class ManaFluxRule_Sunlight : ManaFluxRule
    {
        public FloatRange manaFromSunlightRange;

        public override IntRange ApproximateManaFlux => new IntRange((int)manaFromSunlightRange.min, (int)manaFromSunlightRange.max);

        public override float CalcManaFlux(ArcanePlant plant, int ticks)
        {
            if (plant.Map.roofGrid.Roofed(plant.Position))
            {
                return 0f;
            }

            return manaFromSunlightRange.LerpThroughRange(plant.Map.skyManager.CurSkyGlow) / 60000f * ticks;
        }
    }
}
