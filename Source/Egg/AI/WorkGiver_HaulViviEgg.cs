using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_HaulViviEgg : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.listerThings
                .ThingsMatching(ThingRequest.ForDef(VVThingDefOf.VV_ViviEgg))
                .Where(t =>
                {
                    if (!t.Spawned || t.IsForbidden(pawn)) { return false; }

                    var comp = t.TryGetComp<CompViviHatcher>();
                    if (comp == null || comp.TemperatureDamaged)
                    {
                        return false;
                    }

                    return true;
                });
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            var eggs = pawn.Map.listerThings
                .ThingsMatching(ThingRequest.ForDef(VVThingDefOf.VV_ViviEgg))
                .Where(t => t.Spawned && !t.IsForbidden(pawn));

            return !eggs.Any();
        }

        public override Job JobOnThing(Pawn pawn, Thing egg, bool forced = false)
        {
            if (egg.def != VVThingDefOf.VV_ViviEgg) { return null; }

            var comp = egg.TryGetComp<CompViviHatcher>();
            if (comp == null || comp.TemperatureDamaged) { return null; }

            if (!pawn.CanReserveAndReach(egg, PathEndMode.ClosestTouch, Danger.Deadly)) { return null; }

            var allAcceptibleHatchery = pawn.Map.listerBuildings.AllBuildingsColonistOfDef(VVThingDefOf.VV_ViviHatchery)
                .Where(v => v is ViviEggHatchery hatchery && hatchery.CanLayHere)
                .OrderBy(v => pawn.Position.DistanceToSquared(v.Position))
                .ToList();

            if (!allAcceptibleHatchery.Any()) { return null; }

            foreach (var candidate in allAcceptibleHatchery)
            {
                if (pawn.CanReach(candidate, PathEndMode.OnCell, Danger.Deadly))
                {
                    var job = JobMaker.MakeJob(VVJobDefOf.VV_HaulViviEgg, egg, candidate);
                    job.count = 1;

                    return job;
                }
            }

            return null;
        }
    }
}
