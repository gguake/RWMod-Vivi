using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompManaHeatPusher : CompHeatPusher, IArcanePlantFunctionProvider
    {
        public CompMana ManaComp
        {
            get
            {
                if (_manaComp == null) { _manaComp = parent.GetComp<CompMana>(); }
                return _manaComp;
            }
        }
        private CompMana _manaComp;

        public override bool ShouldPushHeatNow
        {
            get
            {
                if (!parent.Spawned)
                {
                    return false;
                }

                if (!base.ShouldPushHeatNow) { return false; }

                return ManaComp.Active;
            }
        }

        public IEnumerable<string> GetFunctionDescriptions()
        {
            if (Props.heatPerSecond >= 0f)
            {
                yield return LocalizeString_PlantFunction.VV_PlantFunction_HeatPush.Translate(
                    Props.heatPerSecond.ToString("0.#"),
                    Props.heatPushMaxTemperature.ToStringTemperature("F0"));
            }
            else
            {
                yield return LocalizeString_PlantFunction.VV_PlantFunction_HeatAbsorb.Translate(
                    (-Props.heatPerSecond).ToString("0.#"),
                    Props.heatPushMinTemperature.ToStringTemperature("F0"));
            }
        }
    }
}
