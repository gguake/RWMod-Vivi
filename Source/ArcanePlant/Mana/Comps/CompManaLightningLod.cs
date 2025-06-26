using Verse;

namespace VVRace
{
    public class CompProperties_ManaLightningLod : CompProperties
    {
        public int mana;

        public CompProperties_ManaLightningLod()
        {
            compClass = typeof(CompManaLightningLod);
        }
    }

    public class CompManaLightningLod : ThingComp
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

        public CompProperties_ManaLightningLod Props => (CompProperties_ManaLightningLod)props;

        public bool Active => parent.Spawned && !parent.Position.Roofed(parent.Map) && ManaComp.Active;

        public void OnLightningStrike()
        {
            parent.Map.GetComponent<ManaGrid>().AddMana(parent.Position, Props.mana);
        }
    }
}
