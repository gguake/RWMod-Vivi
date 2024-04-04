using RimWorld;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_BatteryStored : ManaFluxRule
    {
        public FloatRange manaFromStoredEnergy;

        public override IntRange ApproximateManaFlux => new IntRange((int)manaFromStoredEnergy.min, (int)manaFromStoredEnergy.max);

        public override float CalcManaFlux(ManaAcceptor plant, int ticks)
        {
            var batteryComp = plant.TryGetComp<CompPowerBattery>();
            if (batteryComp != null)
            {
                return manaFromStoredEnergy.LerpThroughRange(batteryComp.StoredEnergyPct) / 60000f * ticks;
            }

            return 0f;
        }
    }
}
