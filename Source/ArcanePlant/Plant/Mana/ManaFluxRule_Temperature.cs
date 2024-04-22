using Verse;

namespace VVRace
{
    public class ManaFluxRule_Temperature : ManaFluxRule
    {
        public FloatRange activeTemperatureRange;
        public FloatRange manaFromTemperatureRange;
        public bool manaLerpReversed = false;

        public override IntRange ApproximateManaFlux => new IntRange((int)manaFromTemperatureRange.min, (int)manaFromTemperatureRange.max);

        public override float CalcManaFlux(ManaAcceptor plant, int ticks)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0f; }

            var temperature = plant.AmbientTemperature;
            if (activeTemperatureRange.IncludesEpsilon(temperature))
            {
                var t = activeTemperatureRange.InverseLerpThroughRange(temperature);
                return manaFromTemperatureRange.LerpThroughRange(manaLerpReversed ? 1 - t : t) / 60000f * ticks;
            }
            else
            {
                return 0f;
            }
        }
    }
}
