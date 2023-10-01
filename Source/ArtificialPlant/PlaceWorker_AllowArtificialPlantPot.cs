using Verse;

namespace VVRace
{
    public class PlaceWorker_AllowArtificialPlantPot : PlaceWorker
    {
        public override bool IsBuildDesignatorVisible(BuildableDef def)
        {
            return false;
        }

        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            var result = other is ThingDef thingDef && typeof(ArtificialPlantPot).IsAssignableFrom(thingDef.thingClass);
            return result;
        }
    }

}
