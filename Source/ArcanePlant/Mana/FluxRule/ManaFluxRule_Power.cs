using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_Power : ManaFluxRule
    {
        public float powerManaRatio;
        public int min;
        public int max;

        public override IntRange FluxRangeForDisplay => new IntRange(min, max);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Wind_Desc.Translate(max.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            var compPower = thing.TryGetComp<CompPower>();
            if (compPower == null) { return 0; }

            return (int)Mathf.Clamp(compPower.PowerNet.CurrentEnergyGainRate() * 60000 * powerManaRatio, min, max);
        }
    }
}
