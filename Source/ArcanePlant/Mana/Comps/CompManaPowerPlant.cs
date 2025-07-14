using RimWorld;

namespace VVRace
{
    public class CompManaPowerPlant : CompPowerPlant
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

        protected override float DesiredPowerOutput => ManaComp.Active ? -Props.PowerConsumption * ManaComp.StoredPct : 0f;
    }
}
