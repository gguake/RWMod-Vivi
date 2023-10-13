using Verse;

namespace VVRace
{
    public class EnergyRule_Temperature : EnergyRule
    {
        public FloatRange activeTemperatureRange;
        public FloatRange energyByTemperatureRange;
        public bool energyLerpReversed = false;

        public override IntRange ApproximateEnergy => new IntRange((int)energyByTemperatureRange.min, (int)energyByTemperatureRange.max);

        public override float CalcEnergy(ArtificialPlant plant, int ticks)
        {
            var temperature = plant.AmbientTemperature;
            if (activeTemperatureRange.IncludesEpsilon(temperature))
            {
                var t = activeTemperatureRange.InverseLerpThroughRange(temperature);
                return energyByTemperatureRange.LerpThroughRange(energyLerpReversed ? 1 - t : t) / 60000f * ticks;
            }
            else
            {
                return 0f;
            }
        }
    }
}
