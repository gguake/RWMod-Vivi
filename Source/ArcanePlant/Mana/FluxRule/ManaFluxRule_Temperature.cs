using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_Temperature : ManaFluxRule
    {
        public FloatRange activeTemperatureRange;
        public FloatRange manaFromTemperatureRange;
        public bool manaLerpReversed = false;

        public override IntRange FluxRangeForDisplay => new IntRange((int)manaFromTemperatureRange.min, (int)manaFromTemperatureRange.max);

        public override string GetRuleString()
        {
            if (manaLerpReversed)
            {
                return LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Temperature_Desc.Translate(
                    activeTemperatureRange.TrueMin.ToStringTemperature(),
                    activeTemperatureRange.TrueMax.ToStringTemperature(),
                    manaFromTemperatureRange.TrueMax.ToString("+0;-#"),
                    manaFromTemperatureRange.TrueMin.ToString("+0;-#"));
            }
            else
            {
                return LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Temperature_Desc.Translate(
                    activeTemperatureRange.TrueMin.ToStringTemperature(),
                    activeTemperatureRange.TrueMax.ToStringTemperature(),
                    manaFromTemperatureRange.TrueMin.ToString("+0;-#"),
                    manaFromTemperatureRange.TrueMax.ToString("+0;-#"));
            }
        }

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            var temperature = thing.AmbientTemperature;
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
