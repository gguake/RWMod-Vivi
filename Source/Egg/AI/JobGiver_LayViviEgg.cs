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

            if (pawn.Faction == Faction.OfPlayerSilentFail && TryGetBestEggBox(pawn, out var eggBox))
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
                validator: (thing) =>
                {
                    if (!thing.Spawned || thing.IsForbidden(pawn) || !pawn.CanReserve(thing))
                    {
                        return false;
                    }

                    return thing is ViviEggHatchery hatchery && hatchery.CanLayHere;
                }, 
                priorityGetter: (thing) => -pawn.Position.DistanceToSquared(thing.Position),
                minRegions: 10);

            return eggBox != null;
        }
    }
}
