using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_AIWaitWithEscort : ThinkNode_JobGiver
    {
        private const float RandomCellNearRadius = 1.9f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn escortTarget = pawn.GetMindLinkMaster();
            if (escortTarget == null || escortTarget.Awake())
            {
                return null;
            }

            var destination = CanUseCell(pawn.Position, pawn) ? 
                pawn.Position : 
                GetWaitDest(escortTarget.Position, pawn);

            if (destination.IsValid)
            {
                Job job = JobMaker.MakeJob(JobDefOf.Wait_WithSleeping, destination, escortTarget);
                job.expiryInterval = 120;
                job.expireRequiresEnemiesNearby = true;
                return job;
            }

            return null;
        }

        private IntVec3 GetWaitDest(IntVec3 root, Pawn pawn)
        {
            if (CellFinder.TryFindRandomReachableCellNear(
                root,
                pawn.Map, 
                RandomCellNearRadius, 
                TraverseParms.For(pawn), 
                (IntVec3 c) => CanUseCell(c, pawn), 
                null, 
                out var result))
            {
                return result;
            }

            return IntVec3.Invalid;
        }

        private bool CanUseCell(IntVec3 cell, Pawn pawn)
        {
            var map = pawn.Map;
            if (cell.Standable(map) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly) && pawn.CanReserve(cell))
            {
                return cell.GetDoor(map) == null;
            }

            return false;
        }
    }
}
