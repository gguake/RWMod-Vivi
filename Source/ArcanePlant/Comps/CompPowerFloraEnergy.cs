using RimWorld;

namespace VVRace
{
    public class CompPowerFloraEnergy : CompPowerTrader
    {
        private ArcanePlant _cached;
        public ArcanePlant ArcanePlant
        {
            get
            {
                if (_cached == null)
                {
                    _cached = parent as ArcanePlant;
                }

                return _cached;
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            PowerOutput = ArcanePlant.EnergyChargeRatio * (-Props.PowerConsumption);
        }
    }
}
