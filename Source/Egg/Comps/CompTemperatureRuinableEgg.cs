using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class CompTemperatureRuinableEgg : CompTemperatureRuinable
    {
        private const float RecoverTicks = 300000f;

        // 바닐라 CompTickRare는 CompTick을 한 번만 불러 1틱 분량만 더하므로, base를 부르지 않고 지난 틱 수만큼 직접 계산한다.
        public override void CompTick()
        {
            DoTicks(1);
        }

        public override void CompTickRare()
        {
            DoTicks(GenTicks.TickRareInterval);
        }

        private void DoTicks(int ticks)
        {
            // 완전히 손상된 알은 회복하지 않는다.
            if (Ruined) { return; }

            float ambientTemperature = parent.AmbientTemperature;
            if (ambientTemperature > Props.maxSafeTemperature)
            {
                ruinedPercent += (ambientTemperature - Props.maxSafeTemperature) * Props.progressPerDegreePerTick * ticks;
            }
            else if (ambientTemperature < Props.minSafeTemperature)
            {
                ruinedPercent += (Props.minSafeTemperature - ambientTemperature) * Props.progressPerDegreePerTick * ticks;
            }
            else if (ruinedPercent > 0f)
            {
                ruinedPercent = Mathf.Max(0f, ruinedPercent - ticks / RecoverTicks);
            }

            if (ruinedPercent >= 1f)
            {
                ruinedPercent = 1f;
                parent.BroadcastCompSignal(RuinedSignal);
            }
        }
    }
}
