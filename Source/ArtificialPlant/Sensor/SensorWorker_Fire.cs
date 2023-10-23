using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class SensorWorker_Fire : SensorWorker
    {
        public override bool Detected(Thing thing, float radius)
        {
            var closest = GenClosest.ClosestThingReachable(
                thing.Position,
                thing.Map,
                ThingRequest.ForDef(ThingDefOf.Fire),
                PathEndMode.OnCell,
                TraverseParms.For(TraverseMode.NoPassClosedDoors),
                radius);

            return closest != null;
        }
    }
}
