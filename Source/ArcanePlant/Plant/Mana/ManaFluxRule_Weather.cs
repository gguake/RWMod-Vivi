using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_Weather : ManaFluxRule
    {
        public HashSet<WeatherDef> weatherDefs;
        public float mana;

        public override IntRange ApproximateManaFlux => new IntRange(0, (int)mana);

        public override float CalcManaFlux(ManaAcceptor plant, int ticks)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0f; }

            if (weatherDefs != null && weatherDefs.Contains(plant.Map.weatherManager.curWeather))
            {
                return mana / 60000f * ticks;
            }

            return 0f;
        }
    }
}
