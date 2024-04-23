using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_Temperature : ManaFluxRule
    {
        public FloatRange activeTemperatureRange;
        public FloatRange manaFromTemperatureRange;
        public bool manaLerpReversed = false;

        public override IntRange ApproximateManaFlux => new IntRange((int)manaFromTemperatureRange.min, (int)manaFromTemperatureRange.max);

        public override string GetRuleString(bool inverse)
        {
            if (manaLerpReversed)
            {
                return LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Temperature_Desc.Translate(
                    activeTemperatureRange.TrueMin.ToStringTemperature(),
                    activeTemperatureRange.TrueMax.ToStringTemperature(),
                    inverse ? -manaFromTemperatureRange.TrueMax : manaFromTemperatureRange.TrueMax,
                    inverse ? -manaFromTemperatureRange.TrueMin : manaFromTemperatureRange.TrueMin);
            }
            else
            {
                return LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Temperature_Desc.Translate(
                    activeTemperatureRange.TrueMin.ToStringTemperature(),
                    activeTemperatureRange.TrueMax.ToStringTemperature(),
                    inverse ? -manaFromTemperatureRange.TrueMin : manaFromTemperatureRange.TrueMin,
                    inverse ? -manaFromTemperatureRange.TrueMax : manaFromTemperatureRange.TrueMax);
            }
        }

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0; }

            var temperature = plant.AmbientTemperature;
            if (activeTemperatureRange.IncludesEpsilon(temperature))
            {
                var t = activeTemperatureRange.InverseLerpThroughRange(temperature);
                return Mathf.RoundToInt(manaFromTemperatureRange.LerpThroughRange(manaLerpReversed ? 1 - t : t));
            }
            else
            {
                return 0;
            }
        }
    }
}
