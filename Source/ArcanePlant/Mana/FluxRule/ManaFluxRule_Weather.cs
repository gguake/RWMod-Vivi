using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public abstract class ManaFluxRule_Weather : ManaFluxRule
    {
        public bool disableIfRoofed;
        public int mana;

        public override IntRange FluxRangeForDisplay => new IntRange(0, mana);

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            var curWeather = thing.Map.weatherManager.curWeather;
            if (CheckWeather(curWeather))
            {
                if (!disableIfRoofed || !thing.Position.Roofed(thing.Map))
                {
                    return mana;
                }
            }

            return 0;
        }

        protected abstract bool CheckWeather(WeatherDef curWeatherDef);
    }

    public class ManaFluxRule_WeatherRainy : ManaFluxRule_Weather
    {
        public override string GetRuleString() =>
            disableIfRoofed ? 
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_WeatherRainyNoRoof_Desc.Translate(mana.ToString("+0;-#")) :
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_WeatherRainy_Desc.Translate(mana.ToString("+0;-#"));

        protected override bool CheckWeather(WeatherDef curWeatherDef)
        {
            return curWeatherDef.rainRate > 0f;
        }
    }

    public class ManaFluxRule_WeatherSnowy : ManaFluxRule_Weather
    {
        public override string GetRuleString() =>
            disableIfRoofed ?
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_WeatherSnowyNoRoof_Desc.Translate(mana.ToString("+0;-#")) :
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_WeatherSnowy_Desc.Translate(mana.ToString("+0;-#"));

        protected override bool CheckWeather(WeatherDef curWeatherDef)
        {
            return curWeatherDef.snowRate > 0f;
        }
    }
}
