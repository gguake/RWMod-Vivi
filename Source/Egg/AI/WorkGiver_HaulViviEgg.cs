using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_HaulViviEgg : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(VVThingDefOf.VV_ViviEgg);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is ViviEgg egg)) { return false; }

            if (!t.Spawned || t.IsForbidden(pawn) || !pawn.CanReserve(t, ignoreOtherReservations: forced) || t.IsBurning())
            {
                return false;
            }

            var compHatcher = egg.GetComp<CompViviHatcher>();
            if (compHatcher == null || compHatcher.TemperatureDamaged) { return false; }

            var hatchery = FindHatchery(pawn, forced);
            if (hatchery == null) { return false; }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is ViviEgg egg)) { return null; }

            var hatchery = FindHatchery(pawn, forced);
            if (hatchery == null) { return null; }

            var job = HaulAIUtility.HaulToContainerJob(pawn, egg, hatchery);
            job.count = 1;
            return job;
        }

        private ViviEggHatchery FindHatchery(Pawn pawn, bool forced)
        {
            var hatcheryInMap = pawn.Map.listerThings.ThingsOfDef(VVThingDefOf.VV_ViviHatchery);
            foreach (var thing in hatcheryInMap)
            {
                if (thing is ViviEggHatchery hatchery && 
                    hatchery.Spawned &&
                    hatchery.ViviEgg == null &&
                    hatchery.Faction == pawn.Faction &&
                    !hatchery.IsForbidden(pawn) &&
                    !hatchery.IsBurning() &&
                    pawn.Map.designationManager.DesignationOn(hatchery, DesignationDefOf.Deconstruct) == null &&
                    pawn.Map.designationManager.DesignationOn(hatchery, DesignationDefOf.Uninstall) == null &&
                    pawn.CanReserveAndReach(hatchery, PathEndMode.ClosestTouch, MaxPathDanger(pawn), ignoreOtherReservations: forced))
                {
                    return hatchery;
                }
            }

            return null;
        }
    }
}
