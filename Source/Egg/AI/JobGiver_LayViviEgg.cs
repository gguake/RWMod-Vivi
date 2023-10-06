using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_LayViviEgg : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            var compViviEggLayer = pawn.GetCompViviEggLayer();
            return compViviEggLayer != null && compViviEggLayer.CanLayEgg ? 10f : 0f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var compViviEggLayer = pawn.GetCompViviEggLayer();
            if (compViviEggLayer == null || !compViviEggLayer.CanLayEgg)
            {
                return null;
            }

            if (pawn.Faction == Faction.OfPlayer && TryGetBestEggBox(pawn, out var eggBox))
            {
                return JobMaker.MakeJob(VVJobDefOf.VV_LayViviEgg, eggBox);
            }

            return null;
        }

        private bool TryGetBestEggBox(Pawn pawn, out Thing eggBox)
        {
            eggBox = GenClosest.ClosestThing_Regionwise_ReachablePrioritized(
                pawn.Position, 
                pawn.Map, 
                ThingRequest.ForDef(VVThingDefOf.VV_ViviHatchery),
                PathEndMode.OnCell,
                TraverseParms.For(pawn, Danger.Some), 
                maxDistance: 30f, 
                validator: (thing) =>
                {
                    if (!thing.Spawned || thing.IsForbidden(pawn) || !pawn.CanReserve(thing) || !pawn.Position.InHorDistOf(thing.Position, 30f) || thing.Faction != pawn.Faction)
                    {
                        return false;
                    }

                    var hatchery = thing as ViviEggHatchery;
                    if (hatchery != null && hatchery.CanLayHere)
                    {
                        return true;
                    }

                    return false;
                }, 
                priorityGetter: (thing) =>
                {
                    var hatchery = thing as ViviEggHatchery;
                    if (hatchery != null && hatchery.CanLayHere)
                    {
                        return 0f;
                    }

                    return 1f;
                }, 
                minRegions: 10);

            return eggBox != null;
        }
    }
}
