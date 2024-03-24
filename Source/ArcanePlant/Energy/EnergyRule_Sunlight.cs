using Verse;

namespace VVRace
{
    public class EnergyRule_Sunlight : EnergyRule
    {
        public FloatRange energyFromSunlightRange;

        public override IntRange ApproximateEnergy => new IntRange((int)energyFromSunlightRange.min, (int)energyFromSunlightRange.max);

        public override float CalcEnergy(ArcanePlant plant, int ticks)
        {
            if (plant.Map.roofGrid.Roofed(plant.Position))
            {
                return 0f;
            }

            return energyFromSunlightRange.LerpThroughRange(plant.Map.skyManager.CurSkyGlow) / 60000f * ticks;
        }
    }
}
