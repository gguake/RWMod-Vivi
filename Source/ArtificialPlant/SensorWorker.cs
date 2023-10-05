using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public abstract class SensorWorker
    {
        public abstract bool Detected(Thing thing, float radius);
    }

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
