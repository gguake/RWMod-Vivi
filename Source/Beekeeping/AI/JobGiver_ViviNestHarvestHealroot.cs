using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 약이 모자라면 둥지 근처의 다 자란 약뿌리(야생·재배)를 바닐라 수확 잡으로 거둔다(거둔 약초는 수확 알림에서 둥지 몫이 된다).
    // 플레이어 재배 구역의 약뿌리는 건드리지 않는다. 우선도는 약이 목표에 비해 모자란 정도다(보유량 균형).
    public class JobGiver_ViviNestHarvestHealroot : ThinkNode_JobGiver
    {
        private const float SearchRadius = 24f;

        public override float GetPriority(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Growing)) { return 0f; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return 0f; }

            return nest.Production.MedicineNeed;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Growing)) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }
            if (nest.Production.MedicineNeed <= 0f) { return null; }

            var map = pawn.Map;
            var plant = GenClosest.ClosestThingReachable(
                nest.NestCell,
                map,
                ThingRequest.ForGroup(ThingRequestGroup.Plant),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some),
                SearchRadius,
                t => t is Plant p && p.def.plant.harvestedThingDef == ThingDefOf.MedicineHerbal && p.HarvestableNow
                    && map.zoneManager.ZoneAt(p.Position) == null && !p.IsForbidden(pawn) && pawn.CanReserve(p));

            return plant != null ? JobMaker.MakeJob(JobDefOf.Harvest, plant) : null;
        }
    }
}
