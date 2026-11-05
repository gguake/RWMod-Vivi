using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 마나 비료가 모자라면 둥지 근처 비옥한 흙을 파서 만든다(작업대 없이 하므로 느리고 적다).
    public class JobGiver_ViviNestDigFertilizer : ThinkNode_JobGiver
    {
        private const float SearchRadius = 10f;
        private const float MinFertility = 0.7f;
        private const int CellTries = 20;

        // 마나 비료가 목표에 비해 모자란 정도
        public override float GetPriority(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || !pawn.DevelopmentalStage.Adult()) { return 0f; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return 0f; }

            return nest.Production.FertilizerNeed;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || !pawn.DevelopmentalStage.Adult()) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }
            if (nest.Stock.CountOf(VVThingDefOf.VV_Fertilizer) >= ViviNestProduction.FertilizerTarget) { return null; }

            return TryFindDigCell(pawn, nest, out var cell) ? JobMaker.MakeJob(VVJobDefOf.VV_ViviNestDigFertilizer, cell) : null;
        }

        private static bool TryFindDigCell(Pawn pawn, LordJob_ViviNest nest, out IntVec3 cell)
        {
            var map = pawn.Map;
            var origin = nest.Territory.OutdoorStorageCells.Count > 0 ? nest.Territory.OutdoorStorageCells[0] : nest.NestCell;
            var radialCount = GenRadial.NumCellsInRadius(SearchRadius);
            for (int i = 0; i < CellTries; ++i)
            {
                cell = origin + GenRadial.RadialPattern[Rand.Range(0, radialCount)];
                if (IsDiggable(pawn, nest, map, cell)) { return true; }
            }

            cell = IntVec3.Invalid;
            return false;
        }

        private static bool IsDiggable(Pawn pawn, LordJob_ViviNest nest, Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)) { return false; }
            if (map.fertilityGrid.FertilityAt(cell) < MinFertility) { return false; }
            if (nest.Territory.IsTerritoryCell(cell) || map.zoneManager.ZoneAt(cell) != null) { return false; }
            if (cell.Roofed(map) || cell.GetEdifice(map) != null) { return false; }

            foreach (var thing in cell.GetThingList(map))
            {
                if (thing is Building || thing is Pawn || thing.def.category == ThingCategory.Item) { return false; }
                if (thing is Plant plant && !plant.def.plant.IsTree && plant.def.plant.harvestedThingDef != null) { return false; }
                if (thing is Plant tree && tree.def.plant.IsTree) { return false; }
            }

            return !cell.IsForbidden(pawn) && pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some);
        }
    }
}
