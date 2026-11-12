using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestLoaf : ThinkNode_JobGiver
    {
        private const int LoafTicks = 1500;
        private const int OutdoorLoafRadius = 8;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null) { return null; }

            var current = ThinkNode_ViviNestPlay.CurrentPlay(pawn, this);
            if (current != null)
            {
                var cell = current.targetA.Cell;
                var stay = ThinkNode_ViviNestPlay.Elapsed(current) < LoafTicks && (nest.Territory.IsRoomCell(cell) || ThinkNode_ViviNestPlay.IsOutdoorPlaySafe(pawn, cell));
                return stay ? current : null;
            }

            if (!TryFindLoafCell(pawn, nest, out var loafCell)) { return null; }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ViviNestLoaf, loafCell);
            ThinkNode_ViviNestPlay.SetRethink(job);
            return job;
        }

        private static bool TryFindLoafCell(Pawn pawn, LordJob_ViviNest nest, out IntVec3 cell)
        {
            var map = pawn.Map;
            if (Rand.Bool && ThinkNode_ViviNestPlay.IsOutdoorPlayWeatherSafe(pawn, out var comfortable)
                && CellFinder.TryFindRandomCellNear(nest.NestCell, map, OutdoorLoafRadius,
                    c => nest.Planner.IsFreeOutdoorCell(c, allowPlants: true) && comfortable.Includes(GenTemperature.GetTemperatureForCell(c, map))
                        && pawn.CanReserveAndReach(c, PathEndMode.OnCell, Danger.None), out cell))
            {
                return true;
            }

            return nest.TryFindRoomCell(pawn, nest.ComfortableRooms(pawn), out cell);
        }
    }
}
