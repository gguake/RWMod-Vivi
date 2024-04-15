using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_HaulToDreamumAltar : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(VVThingDefOf.VV_DreamumAltar);

        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_DreamumAltar altar) || !altar.RequireDreamum) { return false; }

            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, ignoreOtherReservations: forced)) { return false; }

            var dreamum = FindDreamum(pawn);
            if (dreamum == null)
            {
                JobFailReason.Is(LocalizeString_Etc.VV_JobFailReasonNoIngredients.Translate());
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_DreamumAltar altar) || !altar.RequireDreamum) { return null; }

            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, ignoreOtherReservations: forced)) { return null; }

            var dreamum = FindDreamum(pawn);
            if (dreamum != null)
            {
                var job = HaulAIUtility.HaulToContainerJob(pawn, dreamum, t);
                job.count = 1;
                return job;
            }

            return null;
        }

        private Thing FindDreamum(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.MinifiedThing),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                validator: (thing) =>
                {
                    var minified = thing.GetInnerIfMinified();
                    if (minified == null || minified.def != VVThingDefOf.VV_Dreamum) { return false; }

                    if (thing.IsForbidden(pawn) || !pawn.CanReserve(thing)) { return false; }

                    return true;
                });
        }
    }
}
