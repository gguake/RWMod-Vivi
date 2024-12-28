using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_FortifyHoneycombWall : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (var designation in pawn.Map.designationManager.SpawnedDesignationsOfDef(VVDesignationDefOf.VV_FortifyHoneycombWall))
            {
                yield return designation.target.Thing;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
            => !pawn.Map.designationManager.AnySpawnedDesignationOfDef(VVDesignationDefOf.VV_FortifyHoneycombWall);

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.IsForbidden(pawn) || pawn.Map.designationManager.DesignationOn(t, VVDesignationDefOf.VV_FortifyHoneycombWall) == null)
            {
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }

            if (!pawn.CanReserve(t, ignoreOtherReservations: forced))
            {
                return false;
            }

            var ingredients = FindBestIngredients(pawn, 3);
            if (ingredients == null)
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var ingredients = FindBestIngredients(pawn, 3);
            if (ingredients == null) { return null; }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_FortifyHoneycombWall, t);
            job.targetQueueB = ingredients.Select(thing => new LocalTargetInfo(thing)).ToList();

            return job;
        }

        public List<Thing> FindBestIngredients(Pawn pawn, int requiredCount, int regionDistance = 999999)
        {
            int accumulated = 0;
            var ingredients = new List<Thing>();

            RegionTraverser.BreadthFirstTraverse(
                pawn.Position.GetRegion(pawn.Map),
                (from, to) => to.Allows(TraverseParms.For(pawn), isDestination: false),
                (region) =>
                {
                    foreach (var thing in region.ListerThings.ThingsOfDef(VVThingDefOf.VV_ViviPolymer))
                    {
                        if (ingredients.Contains(thing) || 
                            thing.IsForbidden(pawn) || 
                            !pawn.CanReserve(thing) || 
                            !ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, region, PathEndMode.ClosestTouch, pawn))
                        {
                            continue;
                        }

                        ingredients.Add(thing);
                        accumulated += thing.stackCount;
                        if (accumulated >= requiredCount)
                        {
                            return true;
                        }
                    }

                    return false;
                },
                maxRegions: regionDistance);

            if (accumulated >= requiredCount) { return ingredients; }

            return null;
        }
    }
}
