using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 원목이 모자라면 둥지 근처의 다 자란 나무를 바닐라 베기 잡으로 벤다(나온 원목은 잡이 끝날 때 둥지 몫이 된다).
    // 반경 안에 벨 나무가 없으면 자라는 나무가 벨 만큼 자랄 때(상한 TreeRetryMaxTicks)까지 우선도를 0으로 둔다(ViviNestProduction).
    public class JobGiver_ViviNestCutTree : ThinkNode_JobGiver
    {
        // 벨 나무: 활동 범위 중심(둥지 중심·증축 방 중심) 어디서든 이 반경 안(ViviNestTerritory). 탐색은 둥지 중심에서 활동 범위 끝까지 한다.
        private const float SearchRadius = 24f;
        // 나무 탐색(바닐라 지역 BFS)이 볼 지역 수. 바닐라 기본(30개)을 넘도록 못 찾으면 맵 전체 식물을 훑는 전역 탐색으로 넘어가 1회 140~430 µs였다
        // (실측, 강제 전역 탐색). 반경 판정이 BFS를 활동 범위 끝까지의 지역으로 묶으므로 개수는 넉넉히 두고 전역 탐색은 하지 않는다.
        // 시험 맵의 반경 24칸 지역은 22~29개였다.
        private const int SearchRegionsMax = 1000;

        // 나무를 벨 수 있는 둥지(GetPriority·TryGiveJob 공통 조건): 정착했고, 아기가 아니며 베기를 할 수 있고, 바깥 일을 쉬지 않으며,
        // 나무를 기다리는 중이 아니고 원목이 목표보다 적을 때
        private static LordJob_ViviNest WorkingNest(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)) { return null; }
            if (BeekeepingUtility.OutdoorWorkPaused(pawn)) { return null; }
            var production = nest.Production;
            if (production.TreeWaiting || nest.Stock.CountOf(ThingDefOf.WoodLog) >= production.CachedWoodStockTarget) { return null; }

            return nest;
        }

        // 원목이 목표에 비해 모자란 정도
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

            // 비비와 상관없이 벨 수 있는 나무가 있었는지, 덜 자란 나무가 가장 먼저 벨 만큼 자랄 틱(모르면 -1)
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
                    if (!territory.InActivityRange(plant.Position, SearchRadius)) { return false; }
                    if (map.zoneManager.ZoneAt(plant.Position) != null) { return false; }
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

            // 벨 나무가 아예 없을 때만 둥지 전체가 기다린다(있는데 이 비비가 쓰지 못한 것이면 다른 구성원은 벨 수 있다).
            if (!anyTarget) { production.Notify_NoTree(readyTick); }
            return null;
        }
    }
}
