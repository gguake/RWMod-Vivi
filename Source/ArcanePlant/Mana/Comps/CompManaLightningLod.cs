using System.Collections.Generic;
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

    public class CompManaLightningLod : ThingComp, IArcanePlantFunctionProvider
    {
        public IEnumerable<string> GetFunctionDescriptions()
        {
            yield return LocalizeString_PlantFunction.VV_PlantFunction_LightningRod.Translate(
                Props.mana.ToString());
        }

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
            parent.Map.GetManaComponent().ChangeEnvironmentMana(parent.Position, Props.mana);
        }
    }
}
