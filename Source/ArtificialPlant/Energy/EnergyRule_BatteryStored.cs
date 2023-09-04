using RimWorld;
using Verse;

namespace VVRace
{
    public class EnergyRule_BatteryStored : EnergyRule
    {
        public FloatRange energyByBatteryStored;

        public override IntRange ApproximateEnergy => new IntRange((int)energyByBatteryStored.min, (int)energyByBatteryStored.max);

        public override float CalcEnergy(ArtificialPlant plant, int ticks)
        {
            var batteryComp = plant.TryGetComp<CompPowerBattery>();
            if (batteryComp != null)
            {
                return energyByBatteryStored.LerpThroughRange(batteryComp.StoredEnergyPct) / 60000f * ticks;
            }

            return 0f;
        }
    }
}
