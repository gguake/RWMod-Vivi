using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompTemperatureRuinableEgg : CompTemperatureRuinable
    {
        public override void CompTick()
        {
            base.CompTick();

            DoTicks(1);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            DoTicks(GenTicks.TickRareInterval);
        }

        private void DoTicks(int ticks)
        {
            float ambientTemperature = parent.AmbientTemperature;
            if (ruinedPercent > 0f && ambientTemperature <= Props.maxSafeTemperature && ambientTemperature >= Props.minSafeTemperature)
            {
                ruinedPercent = Mathf.Clamp01(ruinedPercent + ticks / 300000f);
            }
        }
    }
}
