using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 정원 일: 다 자란 목화 수확, 옷감이 모자라면 목화 심기, 채집할 꽃이 모자라면 채집 장소 둘레에 꽃 심기.
    // 바닐라 재배처럼 자리를 막는 풀은 먼저 베고, 심기·수확은 바닐라 잡(Sow·Harvest)을 구역 없이 쓴다.
    public class JobGiver_ViviNestGarden : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }

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
            if (nest.Stock.CountOf(ThingDefOf.Cloth) >= ViviNestGarden.ClothTarget) { return null; }

            foreach (var cell in nest.Garden.CottonCells)
            {
                var job = SowJob(pawn, cell, VVThingDefOf.Plant_Cotton);
                if (job != null) { return job; }
            }
            return null;
        }

        // 꽃이 모자라면 재채집 대기가 짧은 장미를 먼저, 장미가 못 자라는 곳이면 민들레를 심는다.
        private static Job SowFlowerJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var spot = nest.Production.GatherSpot;
            if (spot == null || nest.Garden.FlowerCount >= ViviNestGarden.FlowerTarget) { return null; }

            var map = pawn.Map;
            var cellCount = GenRadial.NumCellsInRadius(Building_GatherWorkTable.GatherMaxRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = spot.Position + GenRadial.RadialPattern[i];
                if (!nest.Planner.IsFreeOutdoorCell(cell, allowPlants: true)) { continue; }

                var flowerDef = PlantUtility.GrowthSeasonNow(cell, map, VVThingDefOf.Plant_Rose) ? VVThingDefOf.Plant_Rose : ThingDefOf.Plant_Dandelion;
                var job = SowJob(pawn, cell, flowerDef);
                if (job != null)
                {
                    if (job.def == JobDefOf.Sow) { nest.Garden.Notify_FlowerSown(); }
                    return job;
                }
            }
            return null;
        }

        private static Job SowJob(Pawn pawn, IntVec3 cell, ThingDef plantDef)
        {
            var map = pawn.Map;
            if (!cell.InBounds(map) || !PlantUtility.GrowthSeasonNow(cell, map, plantDef)) { return null; }

            var plant = cell.GetPlant(map);
            if (plant != null)
            {
                // 이미 심은 것·꿀 꽃·나무는 두고, 자리를 막는 풀만 벤다.
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
