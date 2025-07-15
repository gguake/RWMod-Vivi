using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_PlantArcaneSeed : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.HaulableEver);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!VVResearchProjectDefOf.VV_ArcanePlantSowing.IsFinished) { return true; }

            return !pawn.MapHeld?.GetComponent<ArcanePlantMapComponent>()?.ArcaneSeeds.Any() ?? true;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.MapHeld?.GetComponent<ArcanePlantMapComponent>()?.ArcaneSeeds ?? Enumerable.Empty<Thing>();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var compArcaneSeed = t.TryGetComp<CompArcaneSeed>();
            if (compArcaneSeed == null || !pawn.CanReserve(t, 1, 1, null, forced))
            {
                return false;
            }

            for (int i = 0; i < compArcaneSeed.SeedlingCells.Count; ++i)
            {
                var cell = compArcaneSeed.SeedlingCells[i];
                if (!pawn.CanReach(cell, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                if (!compArcaneSeed.CanSowAt(cell, t.MapHeld))
                {
                    compArcaneSeed.SeedlingCells.RemoveAt(i); i--;
                    continue;
                }

                var plant = cell.GetPlant(pawn.MapHeld);
                if (plant == null || CanDoCutJob(pawn, plant, forced))
                {
                    return true;
                }
            }

            return false;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var compArcaneSeed = t.TryGetComp<CompArcaneSeed>();
            if (compArcaneSeed == null)
            {
                return null;
            }

            for (int i = 0; i < compArcaneSeed.SeedlingCells.Count; ++i)
            {
                var cell = compArcaneSeed.SeedlingCells[i];
                if (!pawn.CanReach(cell, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                if (!compArcaneSeed.CanSowAt(cell, t.MapHeld))
                {
                    compArcaneSeed.SeedlingCells.RemoveAt(i); i--;
                    continue;
                }

                var plant = cell.GetPlant(pawn.MapHeld);
                if (plant == null)
                {
                    var job = JobMaker.MakeJob(VVJobDefOf.VV_PlantArcaneSeed, t, cell);
                    job.playerForced = forced;
                    job.count = 1;
                    return job;
                }

                if (CanDoCutJob(pawn, plant, forced))
                {
                    return JobMaker.MakeJob(JobDefOf.CutPlant, plant);
                }
            }

            return null;
        }

        private bool CanDoCutJob(Pawn pawn, Thing plant, bool forced)
        {
            if (!pawn.CanReserve(plant, 1, -1, null, forced))
            {
                return false;
            }

            if (!PlantUtility.PawnWillingToCutPlant_Job(plant, pawn))
            {
                return false;
            }

            return true;
        }
    }
}
