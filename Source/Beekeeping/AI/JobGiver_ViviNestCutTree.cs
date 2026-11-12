using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestCutTree : ThinkNode_JobGiver
    {
        private const float SearchRadius = 24f;
        private const int SearchRegionsMax = 1000;

        private static LordJob_ViviNest WorkingNest(Pawn pawn)
        {
            var nest = BeekeepingUtility.OutdoorWorkingNest(pawn, WorkTypeDefOf.PlantCutting);
            if (nest == null) { return null; }
            var production = nest.Production;
            if (production.TreeWaiting || nest.Stock.CountOf(ThingDefOf.WoodLog) >= production.CachedWoodStockTarget) { return null; }

            return nest;
        }

        public override float GetPriority(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            return nest != null ? nest.Production.WoodNeed : 0f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            if (nest == null) { return null; }
            var production = nest.Production;

            var anyTarget = false;
            var readyTick = -1;
            var map = pawn.Map;
            var territory = nest.Territory;
            var tree = GenClosest.ClosestThingReachable(
                nest.NestCell,
                map,
                ThingRequest.ForGroup(ThingRequestGroup.Plant),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some),
                territory.ActivityReachFrom(nest.NestCell),
                t =>
                {
                    if (!(t is Plant plant) || !plant.def.plant.IsTree || plant.def.plant.harvestedThingDef != ThingDefOf.WoodLog) { return false; }
                    if (plant.def.plant.treeCategory == TreeCategory.Super || plant.Position.GetPlantToGrowSettable(map) != null) { return false; }
                    if (!territory.InActivityRange(plant.Position, SearchRadius)) { return false; }
                    if (!plant.HarvestableNow)
                    {
                        readyTick = BeekeepingUtility.EarlierTick(readyTick, BeekeepingUtility.HarvestReadyTick(plant, ViviNestProduction.TreeRetryMaxTicks));
                        return false;
                    }

                    anyTarget = true;
                    return !plant.IsForbidden(pawn) && pawn.CanReserve(plant);
                },
                searchRegionsMax: SearchRegionsMax);

            if (tree != null) { return JobMaker.MakeJob(JobDefOf.CutPlant, tree); }

            if (!anyTarget) { production.Notify_NoTree(readyTick); }
            return null;
        }
    }
}
