using Verse;

namespace VVRace
{
    public class CompProperties_ManaGenerator : CompProperties
    {
        public float mana;

        public CompProperties_ManaGenerator()
        {
            compClass = typeof(CompManaGenerator);
        }
    }

    public class CompManaGenerator : ThingComp
    {
        public CompProperties_ManaGenerator Props => (CompProperties_ManaGenerator)props;

    }
}
