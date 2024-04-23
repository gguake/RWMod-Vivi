using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_Wind : ManaFluxRule
    {
        public SimpleCurve manaFromWindSpeed;

        public override IntRange ApproximateManaFlux => new IntRange((int)manaFromWindSpeed.MinY, (int)manaFromWindSpeed.MaxY);

        public override string GetRuleString(bool inverse) =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Wind_Desc.Translate(
                inverse ? -(int)manaFromWindSpeed.MaxY : (int)manaFromWindSpeed.MaxY);

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            if (!plant.Spawned || plant.Destroyed || !plant.IsOutside()) { return 0; }

            return Mathf.RoundToInt(manaFromWindSpeed.Evaluate(plant.Map.windManager.WindSpeed));
        }
    }
}
