using Verse;
using Verse.AI;

namespace VVRace
{

    public class SensorWorker_Mechanoid : SensorWorker
    {
        public override bool Detected(Thing thing, float radius)
        {
            var closest = GenClosest.ClosestThingReachable(
                thing.Position,
                thing.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.OnCell,
                TraverseParms.For(TraverseMode.NoPassClosedDoors),
                radius,
                validator: (Thing t) =>
                {
                    return t is Pawn pawn && pawn.RaceProps != null && pawn.RaceProps.IsMechanoid;
                });

            return closest != null;
        }
    }
}
