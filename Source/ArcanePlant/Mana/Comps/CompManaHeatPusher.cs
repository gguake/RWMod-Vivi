using Verse;

namespace VVRace
{
    public class CompManaHeatPusher : CompHeatPusher
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

        public override bool ShouldPushHeatNow
        {
            get
            {
                if (!base.ShouldPushHeatNow) { return false; }

                return ManaComp.Active;
            }
        }
    }
}
