using RimWorld;
using Verse;

namespace VVRace
{
    public class Designator_PlantSeed : Designator_Place
    {
        public override BuildableDef PlacingDef => VVThingDefOf.VV_ArcanePlantSeedling;

        public override ThingStyleDef ThingStyleDefForPreview => null;

        public override ThingDef StuffDef => null;

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            // TODO
        }
    }
}
