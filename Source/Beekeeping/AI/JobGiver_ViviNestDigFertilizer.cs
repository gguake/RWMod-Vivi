using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestDigFertilizer : ThinkNode_JobGiver
    {
        private const float SearchRadius = 10f;
        private const int CellTries = 20;

        private static LordJob_ViviNest WorkingNest(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || !pawn.DevelopmentalStage.Adult()) { return null; }
            return BeekeepingUtility.OutdoorWorkPaused(pawn) ? null : nest;
        }

        public override float GetPriority(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            return nest != null ? nest.Production.FertilizerNeed : 0f;
        }

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
