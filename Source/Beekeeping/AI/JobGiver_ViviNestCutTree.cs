using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 원목이 모자라면 둥지 근처의 다 자란 나무를 바닐라 베기 잡으로 벤다(나온 원목은 잡이 끝날 때 둥지 몫이 된다).
    public class JobGiver_ViviNestCutTree : ThinkNode_JobGiver
    {
        private const float SearchRadius = 24f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || !pawn.DevelopmentalStage.Adult()) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }
            if (nest.Stock.CountOf(ThingDefOf.WoodLog) >= nest.Production.Target(ThingDefOf.WoodLog, ViviNestProduction.WoodTarget)) { return null; }

            var map = pawn.Map;
            var tree = GenClosest.ClosestThingReachable(
                nest.NestCell,
                map,
                ThingRequest.ForGroup(ThingRequestGroup.Plant),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some),
                SearchRadius,
                t => t is Plant plant && plant.def.plant.IsTree && plant.def.plant.harvestedThingDef == ThingDefOf.WoodLog
                    && plant.HarvestableNow && map.zoneManager.ZoneAt(plant.Position) == null
                    && !plant.IsForbidden(pawn) && pawn.CanReserve(plant));

            return tree != null ? JobMaker.MakeJob(JobDefOf.CutPlant, tree) : null;
        }
    }
}
