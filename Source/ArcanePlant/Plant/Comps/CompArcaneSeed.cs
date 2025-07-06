using System.Collections.Generic;
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
        public static Designator_PlantSeed DesignatorPlantSeed
        {
            get
            {
                if (_designatorPlantSeed == null)
                {
                    _designatorPlantSeed = new Designator_PlantSeed();
                }
                return _designatorPlantSeed;
            }
        }
        private static Designator_PlantSeed _designatorPlantSeed;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return DesignatorPlantSeed;
        }
    }
}
