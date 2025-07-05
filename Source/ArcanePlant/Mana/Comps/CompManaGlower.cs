using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_ManaGlower : CompProperties_Glower
    {
        public bool glowOnlyRoofed;

        public CompProperties_ManaGlower() : base()
        {
            compClass = typeof(CompManaGlower);
        }
    }

    public class CompManaGlower : CompGlower, IManaChangeEventReceiver
    {
        private new CompProperties_ManaGlower Props => (CompProperties_ManaGlower)props;

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

                if (Props.glowOnlyRoofed && !parent.Position.Roofed(parent.Map)) { return false; }

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
