using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class CompManaPowerPlant : CompPowerPlant, IArcanePlantFunctionProvider
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

        protected override float DesiredPowerOutput => ManaComp.Active ? -Props.PowerConsumption * ManaComp.StoredPct : 0f;

        public IEnumerable<string> GetFunctionDescriptions()
        {
            yield return LocalizeString_PlantFunction.VV_PlantFunction_PowerPlant.Translate(
                (-Props.PowerConsumption).ToString("F0"));
        }
    }
}
