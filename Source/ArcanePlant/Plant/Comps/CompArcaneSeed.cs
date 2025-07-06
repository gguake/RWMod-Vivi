using Verse;

namespace VVRace
{
    public class CompProperties_ArcaneSeed : CompProperties
    {
        public ThingDef targetPlantDef;

        public CompProperties_ArcaneSeed()
        {
            compClass = typeof(CompArcaneSeed);
        }
    }

    public class CompArcaneSeed : ThingComp
    {

    }
}
