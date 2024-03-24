using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class EnergyRule_Weather : EnergyRule
    {
        public HashSet<WeatherDef> weatherDefs;
        public float energy;

        public override IntRange ApproximateEnergy => new IntRange(0, (int)energy);

        public override float CalcEnergy(ArcanePlant plant, int ticks)
        {
            if (weatherDefs != null && weatherDefs.Contains(plant.Map.weatherManager.curWeather))
            {
                return energy / 60000f * ticks;
            }

            return 0f;
        }
    }
}
