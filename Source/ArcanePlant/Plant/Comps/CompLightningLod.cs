using Verse;

namespace VVRace
{
    public class CompProperties_LightningLod : CompProperties
    {
        public IntRange manaConditionRange;
        public int manaGain;

        public CompProperties_LightningLod()
        {
            compClass = typeof(CompLightningLod);
        }
    }

    public class CompLightningLod : ThingComp
    {
        public CompProperties_LightningLod Props => (CompProperties_LightningLod)props;

        public bool Active => 
            parent is ArcanePlant plant &&
            plant.Spawned &&
            !plant.Position.Roofed(plant.Map) &&
            plant.Mana >= Props.manaConditionRange.min && 
            plant.Mana < Props.manaConditionRange.max;

        public void OnLightningStrike()
        {
            if (parent is ArcanePlant plant)
            {
                plant.AddMana(Props.manaGain);
            }
        }
    }
}
