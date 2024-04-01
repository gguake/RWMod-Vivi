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
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(VVThingDefOf.VV_ViviHatchery);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is ViviEggHatchery hatchery)) { return false; }

            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, ignoreOtherReservations: forced) || t.IsBurning())
            {
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }

            if (hatchery.ViviEgg != null) { return false; }

            var comp = t.TryGetComp<CompViviHatcher>();
            if (comp == null || comp.TemperatureDamaged) { return false; }

            var egg = FindEgg(pawn, forced);
            return egg != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is ViviEggHatchery hatchery)) { return null; }

            var egg = FindEgg(pawn, forced);
            if (egg == null) { return null; }

            var job = HaulAIUtility.HaulToContainerJob(pawn, egg, t);
            job.count = 1;
            return job;
        }

        private ViviEgg FindEgg(Pawn pawn, bool forced)
        {
            var eggsInMap = pawn.Map.listerThings.ThingsOfDef(VVThingDefOf.VV_ViviEgg);
            foreach (var thing in eggsInMap)
            {
                if (thing is ViviEgg egg && egg.Spawned && !egg.IsForbidden(pawn) && pawn.CanReserveAndReach(egg, PathEndMode.ClosestTouch, MaxPathDanger(pawn), ignoreOtherReservations: forced))
                {
                    return egg;
                }
            }

            return null;
        }
    }
}
