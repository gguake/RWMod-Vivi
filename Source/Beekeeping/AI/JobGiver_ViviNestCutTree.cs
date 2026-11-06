using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 원목이 모자라면 둥지 근처의 다 자란 나무를 바닐라 베기 잡으로 벤다(나온 원목은 잡이 끝날 때 둥지 몫이 된다).
    // 반경 안에 벨 나무가 없으면 자라는 나무가 벨 만큼 자랄 때(상한 TreeRetryMaxTicks)까지 우선도를 0으로 둔다(ViviNestProduction).
    public class JobGiver_ViviNestCutTree : ThinkNode_JobGiver
    {
        private const float SearchRadius = 24f;
        // 나무 탐색(바닐라 지역 BFS)이 볼 지역 수. 바닐라 기본(30개)을 넘도록 못 찾으면 맵 전체 식물을 훑는 전역 탐색으로 넘어가 1회 140~430 µs였다
        // (실측, 강제 전역 탐색). 반경 판정이 BFS를 반경 안 지역으로 묶으므로 개수는 넉넉히 두고 전역 탐색은 하지 않는다.
        // 시험 맵의 반경 24칸 지역은 22~29개였다.
        private const int SearchRegionsMax = 1000;

        // 원목이 목표에 비해 모자란 정도
        public override float GetPriority(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)) { return 0f; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return 0f; }
            var production = nest.Production;
            if (production.TreeWaiting || nest.Stock.CountOf(ThingDefOf.WoodLog) >= production.CachedWoodStockTarget) { return 0f; }

            return production.WoodNeed;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }
            var production = nest.Production;
            if (production.TreeWaiting || nest.Stock.CountOf(ThingDefOf.WoodLog) >= production.CachedWoodStockTarget) { return null; }

            // 비비와 상관없이 벨 수 있는 나무가 있었는지, 덜 자란 나무가 가장 먼저 벨 만큼 자랄 틱(모르면 -1)
            var anyTarget = false;
            var readyTick = -1;
            var map = pawn.Map;
            var tree = GenClosest.ClosestThingReachable(
                nest.NestCell,
                map,
                ThingRequest.ForGroup(ThingRequestGroup.Plant),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some),
                SearchRadius,
                t =>
                {
                    if (!(t is Plant plant) || !plant.def.plant.IsTree || plant.def.plant.harvestedThingDef != ThingDefOf.WoodLog) { return false; }
                    if (map.zoneManager.ZoneAt(plant.Position) != null) { return false; }
                    if (!plant.HarvestableNow)
                    {
                        var ready = plant.def.plant.Harvestable ? BeekeepingUtility.GrowthReadyTick(plant, plant.def.plant.harvestMinGrowth, ViviNestProduction.TreeRetryMaxTicks) : -1;
                        if (ready >= 0 && (readyTick < 0 || ready < readyTick)) { readyTick = ready; }
                        return false;
                    }

                    anyTarget = true;
                    return !plant.IsForbidden(pawn) && pawn.CanReserve(plant);
                },
                searchRegionsMax: SearchRegionsMax);

            if (tree != null) { return JobMaker.MakeJob(JobDefOf.CutPlant, tree); }

            // 벨 나무가 아예 없을 때만 둥지 전체가 기다린다(있는데 이 비비가 쓰지 못한 것이면 다른 구성원은 벨 수 있다).
            if (!anyTarget) { production.Notify_NoTree(readyTick); }
            return null;
        }
    }
}
