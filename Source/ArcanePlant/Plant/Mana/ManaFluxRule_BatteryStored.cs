using RimWorld;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_BatteryStored : ManaFluxRule
    {
        public IntRange manaFromStoredEnergy;

        public override IntRange ApproximateManaFlux => new IntRange(manaFromStoredEnergy.min, manaFromStoredEnergy.max);

        public override string GetRuleString(bool inverse) =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_BatteryStored_Desc.Translate(
                inverse ? -manaFromStoredEnergy.TrueMax : manaFromStoredEnergy.TrueMax);

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0; }

            var batteryComp = plant.TryGetComp<CompPowerBattery>();
            if (batteryComp != null)
            {
                return manaFromStoredEnergy.Lerped(batteryComp.StoredEnergyPct);
            }

            return 0;
        }
    }
}
