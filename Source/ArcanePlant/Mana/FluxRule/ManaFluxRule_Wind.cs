using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_Wind : ManaFluxRule
    {
        public SimpleCurve manaFromWindSpeed;

        public override IntRange FluxRangeForDisplay => new IntRange((int)manaFromWindSpeed.MinY, (int)manaFromWindSpeed.MaxY);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Wind_Desc.Translate(((int)manaFromWindSpeed.MaxY).ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed || !thing.IsOutside()) { return 0; }

            return Mathf.RoundToInt(manaFromWindSpeed.Evaluate(thing.Map.windManager.WindSpeed));
        }
    }
}
