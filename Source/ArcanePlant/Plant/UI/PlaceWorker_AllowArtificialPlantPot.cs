using Verse;

namespace VVRace
{
    public class PlaceWorker_AllowArcanePlantPot : PlaceWorker
    {
        public override bool ForceAllowPlaceOver(BuildableDef other)
        {
            var result = other is ThingDef thingDef && typeof(ArcanePlantPot).IsAssignableFrom(thingDef.thingClass);
            return result;
        }
    }

}
