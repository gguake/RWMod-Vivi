﻿using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_FertilizeArtificialPlant : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is ArtificialPlant plant && plant.FertilizeAutoActivated && plant.Energy <= plant.FertilizeAutoThreshold))
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