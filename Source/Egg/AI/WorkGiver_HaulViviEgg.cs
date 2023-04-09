using RimWorld;
using Verse.AI;
using Verse;
using System.Collections.Generic;
using System.Linq;

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

            var allAcceptibleHatchery = pawn.Map.listerBuildings
                .AllBuildingsColonistOfDef(VVThingDefOf.VV_ViviHatchery)
                .Cast<ViviEggHatchery>()
                .Where(v => v.CanLayHere)
                .OrderBy(v => pawn.Position.DistanceToSquared(v.Position))
                .ToList();

            if (!allAcceptibleHatchery.Any()) { return null; }

            ViviEggHatchery targetHatchery = null;
            foreach (var candidate in allAcceptibleHatchery)
            {
                if (pawn.CanReach(candidate, PathEndMode.OnCell, Danger.Deadly))
                {
                    targetHatchery = candidate;
                    break;
                }
            }

            if (targetHatchery == null)
            {
                return null;
            }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_HaulViviEgg, egg, targetHatchery);
            job.count = 1;

            return job;
        }
    }
}
