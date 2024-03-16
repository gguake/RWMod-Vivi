using Verse;

namespace VVRace
{
    public class CompHeatPusherFloraEnergy : CompHeatPusher
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

        public override bool ShouldPushHeatNow
        {
            get
            {
                if (!base.ShouldPushHeatNow) { return false; }

                return ArtificialPlant != null && ArtificialPlant.EnergyChargeRatio > 0f;
            }
        }
    }
}
