using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 마나 비료가 모자라면 둥지 근처 비옥한 흙을 파서 만든다(작업대 없이 하므로 느리고 적다).
    public class JobGiver_ViviNestDigFertilizer : ThinkNode_JobGiver
    {
        private const float SearchRadius = 10f;
        private const int CellTries = 20;

        // 땅 비료를 팔 수 있는 둥지(GetPriority·TryGiveJob 공통 조건): 정착했고, 어른이며, 바깥 일을 쉬지 않을 때
        private static LordJob_ViviNest WorkingNest(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || !pawn.DevelopmentalStage.Adult()) { return null; }
            return BeekeepingUtility.OutdoorWorkPaused(pawn) ? null : nest;
        }

        // 마나 비료가 목표에 비해 모자란 정도
        public override float GetPriority(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            return nest != null ? nest.Production.FertilizerNeed : 0f;
        }

        // 바깥 보관 칸 둘레의 무작위 칸 중 팔 수 있는 칸을 판다.
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            if (nest == null) { return null; }
            if (nest.Stock.CountOf(VVThingDefOf.VV_Fertilizer) >= nest.Production.FertilizerTarget) { return null; }

            var map = pawn.Map;
            var origin = nest.Territory.OutdoorStorageCells.Count > 0 ? nest.Territory.OutdoorStorageCells[0] : nest.NestCell;
            var radialCount = GenRadial.NumCellsInRadius(SearchRadius);
            for (int i = 0; i < CellTries; ++i)
            {
                var cell = origin + GenRadial.RadialPattern[Rand.Range(0, radialCount)];
                if (IsDiggable(pawn, nest, map, cell)) { return JobMaker.MakeJob(VVJobDefOf.VV_ViviNestDigFertilizer, cell); }
            }

            return null;
        }

        // 비옥한 흙의 하한은 씨앗 자리와 같다(JobGiver_ViviNestPlantSeed.MinFertility, ViviNestSiteEvaluator.MinFertility 참고).
        private static bool IsDiggable(Pawn pawn, LordJob_ViviNest nest, Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)) { return false; }
            if (map.fertilityGrid.FertilityAt(cell) < JobGiver_ViviNestPlantSeed.MinFertility) { return false; }
            if (nest.Territory.IsTerritoryCell(cell) || map.zoneManager.ZoneAt(cell) != null) { return false; }
            if (cell.Roofed(map) || cell.GetEdifice(map) != null) { return false; }

            foreach (var thing in cell.GetThingList(map))
            {
                if (thing is Building || thing is Pawn || thing.def.category == ThingCategory.Item) { return false; }
                if (thing is Plant plant && (plant.def.plant.IsTree || plant.def.plant.harvestedThingDef != null)) { return false; }
            }

            return !cell.IsForbidden(pawn) && pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some);
        }
    }
}
