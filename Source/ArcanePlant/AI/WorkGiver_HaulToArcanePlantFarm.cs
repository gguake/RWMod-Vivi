using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_HaulToArcanePlantFarm : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(VVThingDefOf.VV_ArcanePlantFarm);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        private Dictionary<ThingDef, int> _tmpRequiredIngredients = new Dictionary<ThingDef, int>();
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_ArcanePlantFarm arcanePlantFarm)) { return false; }

            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, ignoreOtherReservations: forced) || t.IsBurning() || pawn.Faction != t.Faction)
            {
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }

            if (arcanePlantFarm.Bill == null || arcanePlantFarm.Bill.IsStarted) { return false; }

            try
            {
                foreach (var tdc in arcanePlantFarm.RequiredIngredients)
                {
                    _tmpRequiredIngredients.Add(tdc.ThingDef, tdc.Count);
                }

                var tc = FindIngredients(pawn, arcanePlantFarm);
                if (tc.Thing == null)
                {
                    JobFailReason.Is(LocalizeTexts.JobFailReasonNoIngredients.Translate());
                    return false;
                }

                return true;
            }
            finally
            {
                _tmpRequiredIngredients.Clear();
            }
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_ArcanePlantFarm arcanePlantFarm)) { return null; }

            if (arcanePlantFarm.Bill == null || arcanePlantFarm.Bill.IsStarted) { return null; }
            
            try
            {
                foreach (var tdc in arcanePlantFarm.RequiredIngredients)
                {
                    _tmpRequiredIngredients.Add(tdc.ThingDef, tdc.Count);
                }

                var tc = FindIngredients(pawn, arcanePlantFarm);
                if (tc.Thing != null)
                {
                    var job = HaulAIUtility.HaulToContainerJob(pawn, tc.Thing, t);
                    job.count = Mathf.Min(job.count, tc.Count);
                    return job;
                }

                return null;
            }
            finally
            {
                _tmpRequiredIngredients.Clear();
            }
        }

        private ThingCount FindIngredients(Pawn pawn, Building_ArcanePlantFarm arcanePlaneFarm)
        {
            var found = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                validator: (thing) =>
                {
                    if (thing.IsForbidden(pawn) || !pawn.CanReserve(thing) || thing.stackCount <= 0) { return false; }

                    return _tmpRequiredIngredients.ContainsKey(thing.def);
                });

            if (found == null)
            {
                return default;
            }
            else
            {
                return new ThingCount(found, Mathf.Min(found.stackCount, _tmpRequiredIngredients[found.def]));
            }
        }
    }
}
