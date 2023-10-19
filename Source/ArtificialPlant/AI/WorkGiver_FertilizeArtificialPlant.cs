using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_FertilizeArtificialPlant : WorkGiver_Scanner
    {
        // for performance trick
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.WithCustomRectForSelector);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var plant = t as ArtificialPlant;
            if (plant == null)
            {
                return false;
            }

            if (!forced && !plant.ShouldAutoFertilizeNowIgnoringEnergyPct)
            {
                return false;
            }

            if (!plant.FertilizeAutoActivated || plant.Energy > plant.FertilizeAutoThreshold)
            {
                return false;
            }

            if (!t.Spawned)
            {
                return false;
            }

            if (!pawn.CanReserve(t, ignoreOtherReservations: forced))
            {
                return false;
            }

            if (FindFertilizer(pawn) == null)
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var plant = t as ArtificialPlant;
            if (plant == null) { return null; }

            var fertilizer = FindFertilizer(pawn);
            if (fertilizer == null) { return null; }

            return JobMaker.MakeJob(VVJobDefOf.VV_FertilizeArtificialPlant, t, fertilizer);
        }

        private Thing FindFertilizer(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(VVThingDefOf.VV_Fertilizer),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                validator: thing => !thing.IsForbidden(pawn) && pawn.CanReserve(thing));
        }
    }
}
