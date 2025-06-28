using RimWorld;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_BatteryStored : ManaFluxRule
    {
        public IntRange manaFromStoredEnergy;

        public override IntRange FluxRangeForDisplay => new IntRange(manaFromStoredEnergy.min, manaFromStoredEnergy.max);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_BatteryStored_Desc.Translate(
                manaFromStoredEnergy.TrueMax.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            var batteryComp = thing.TryGetComp<CompPowerBattery>();
            if (batteryComp != null)
            {
                return manaFromStoredEnergy.Lerped(batteryComp.StoredEnergyPct);
            }

            return 0;
        }
    }
}
