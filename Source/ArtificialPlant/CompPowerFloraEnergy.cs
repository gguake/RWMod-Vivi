using RimWorld;

namespace VVRace
{
    public class CompPowerFloraEnergy : CompPowerTrader
    {
        private ArtificialPlant _cachedArtificialPlant;
        public ArtificialPlant ArtificialPlant
        {
            get
            {
                if (_cachedArtificialPlant == null)
                {
                    _cachedArtificialPlant = parent as ArtificialPlant;
                }

                return _cachedArtificialPlant;
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            PowerOutput = ArtificialPlant.EnergyChargeRatio * (-Props.PowerConsumption);
        }
    }
}
