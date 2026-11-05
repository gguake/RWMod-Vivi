using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 놀이 묶음의 빈둥거리기: 편한 기온의 둥지 방(바깥이 편하면 둥지 둘레 바깥 빈 땅도)의 한 자리에 가서 멍하니 서 있거나 곁의 구성원과 어울린다
    // (바닐라 모임의 서 있기 잡). 놀이 시간이 지나면 다른 놀이를 고르고, 바깥 자리는 바깥 놀이를 할 수 없게 되면 그만둔다.
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
                var stay = ThinkNode_ViviNestPlay.Elapsed(current) < LoafTicks && (nest.Territory.IsRoomCell(cell) || ThinkNode_ViviNestPlay.OutdoorPlayOk(pawn, cell));
                return stay ? current : null;
            }

            if (!TryFindLoafCell(pawn, nest, out var loafCell)) { return null; }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ViviNestLoaf, loafCell);
            ThinkNode_ViviNestPlay.SetRethink(job);
            return job;
        }

        // 바깥이 편하면 절반은 둥지 둘레 바깥 빈 땅(공사 자리처럼 둥지 칸·구역·통로를 피한다)에서, 아니면 편한 기온의 둥지 방에서 빈둥거린다.
        private static bool TryFindLoafCell(Pawn pawn, LordJob_ViviNest nest, out IntVec3 cell)
        {
            if (Rand.Bool && ThinkNode_ViviNestPlay.OutdoorPlayOk(pawn, nest.NestCell)
                && CellFinder.TryFindRandomCellNear(nest.NestCell, pawn.Map, OutdoorLoafRadius,
                    c => nest.Planner.IsFreeOutdoorCell(c, allowPlants: true) && ThinkNode_ViviNestPlay.OutdoorPlayOk(pawn, c)
                        && pawn.CanReserveAndReach(c, PathEndMode.OnCell, Danger.None), out cell))
            {
                return true;
            }

            return nest.TryFindRoomCell(pawn, nest.ComfortableRooms(pawn), out cell);
        }
    }
}
