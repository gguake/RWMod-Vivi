using Verse;

namespace VVRace
{
    public class CompManaGlower : CompGlower, IManaChangeEventReceiver
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

        protected override bool ShouldBeLitNow
        {
            get
            {
                if (!base.ShouldBeLitNow) { return false; }

                return ManaComp.Active;
            }
        }

        public void Notify_ManaActivateChanged(bool before, bool current)
        {
            if (parent.Spawned)
            {
                UpdateLit(parent.Map);
            }
        }
    }
}
