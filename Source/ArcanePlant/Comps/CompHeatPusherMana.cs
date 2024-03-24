using Verse;

namespace VVRace
{
    public class CompHeatPusherMana : CompHeatPusher
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

        public override bool ShouldPushHeatNow
        {
            get
            {
                if (!base.ShouldPushHeatNow) { return false; }

                return ArcanePlant != null && ArcanePlant.ManaChargeRatio > 0f;
            }
        }
    }
}
