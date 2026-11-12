using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestGarden : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.OutdoorWorkingNest(pawn);
            if (nest == null) { return null; }

            return HarvestCottonJob(pawn, nest) ?? SowCottonJob(pawn, nest) ?? SowFlowerJob(pawn, nest);
        }

        private static Job HarvestCottonJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var map = pawn.Map;
            foreach (var cell in nest.Garden.CottonCells)
            {
                var plant = cell.GetPlant(map);
                if (plant == null || plant.def != VVThingDefOf.Plant_Cotton || !plant.HarvestableNow) { continue; }
                if (!pawn.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Some)) { continue; }

                return JobMaker.MakeJob(JobDefOf.Harvest, plant);
            }
            return null;
        }

        private static Job SowCottonJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var map = pawn.Map;
            if (nest.Stock.CountUsableFor(VVRecipeDefOf.Make_VV_BasicWorkerCostume) >= ViviNestGarden.ClothTarget) { return null; }

            foreach (var cell in nest.Garden.CottonCells)
            {
                var job = SowJob(pawn, cell, VVThingDefOf.Plant_Cotton);
                if (job != null) { return job; }
            }
            return null;
        }

        private static Job SowFlowerJob(Pawn pawn, LordJob_ViviNest nest)
        {
            if (nest.Garden.FlowerCount >= nest.Garden.FlowerTarget() || nest.Garden.FlowerSpotSearchPaused) { return null; }

            var map = pawn.Map;
            var cells = nest.Territory.ActivityCells;
            var cellCount = nest.Territory.ActivityCellCount(ViviNestProduction.GatherRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = cells[i];
                if (!nest.Planner.IsFreeOutdoorCell(cell, allowPlants: true)) { continue; }

                var flowerDef = PlantUtility.GrowthSeasonNow(cell, map, VVThingDefOf.Plant_Rose) ? VVThingDefOf.Plant_Rose : ThingDefOf.Plant_Dandelion;
                var job = SowJob(pawn, cell, flowerDef);
                if (job != null)
                {
                    if (job.def == JobDefOf.Sow) { nest.Garden.Notify_FlowerSown(); }
                    return job;
                }
            }

            nest.Garden.Notify_NoFlowerSpot();
            return null;
        }

        private static Job SowJob(Pawn pawn, IntVec3 cell, ThingDef plantDef)
        {
            var map = pawn.Map;
            if (!cell.InBounds(map) || !PlantUtility.GrowthSeasonNow(cell, map, plantDef)) { return null; }

            var plant = cell.GetPlant(map);
            if (plant != null)
            {
                if (plant.def == plantDef || plant.def.plant.IsTree || ViviNestGarden.IsHoneyFlowerDef(plant.def)) { return null; }
                if (!plantDef.CanEverPlantAt(cell, map, out _, canWipePlantsExceptTree: true).Accepted) { return null; }
                if (!pawn.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Some) || !PlantUtility.PawnWillingToCutPlant_Job(plant, pawn)) { return null; }

                return JobMaker.MakeJob(JobDefOf.CutPlant, plant);
            }

            if (!plantDef.CanNowPlantAt(cell, map) || PlantUtility.AdjacentSowBlocker(plantDef, cell, map) != null) { return null; }
            if (!pawn.CanReserveAndReach(cell, PathEndMode.Touch, Danger.Some)) { return null; }

            var job = JobMaker.MakeJob(JobDefOf.Sow, cell);
            job.plantDefToSow = plantDef;
            return job;
        }
    }
}
