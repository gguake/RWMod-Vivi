using RimWorld;

namespace VVRace
{
    public class CompPowerMana : CompPowerTrader
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
            if (!parent.Spawned || parent.Destroyed) { return; }

            base.CompTick();

            PowerOutput = ArcanePlant.ManaChargeRatio * (-Props.PowerConsumption);
        }
    }
}
