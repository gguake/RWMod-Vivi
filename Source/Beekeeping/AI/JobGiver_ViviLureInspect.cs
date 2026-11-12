using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class JobGiver_ViviLureInspect : ThinkNode_JobGiver
    {
        private const float FlowerStrollRadius = 12f;
        private const int FlowerStrollTries = 8;
        private const int StrollExpiryTicks = 600;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var lordJob = pawn.GetLord()?.LordJob as LordJob_ViviLureScout;
            var lure = lordJob?.Lure;
            if (lure == null || !lure.Spawned || lure.Map != pawn.Map) { return null; }

            if (!lordJob.HasSniffed(pawn) && pawn.CanReach(lure, PathEndMode.Touch, Danger.Some))
            {
                return JobMaker.MakeJob(VVJobDefOf.VV_ViviInspectLure, lure);
            }

            if (!lordJob.HasVisitedRoom(pawn))
            {
                lordJob.Notify_RoomVisited(pawn);

                var roomCell = lure.SiteReport?.bestRoomCell ?? IntVec3.Invalid;
                var room = roomCell.IsValid ? roomCell.GetRoom(pawn.Map) : null;
                if (room != null && room.Cells.TryRandomElement(c => c.Standable(pawn.Map) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Some), out var visitCell))
                {
                    var job = JobMaker.MakeJob(JobDefOf.Goto, visitCell);
                    job.locomotionUrgency = LocomotionUrgency.Walk;
                    job.expiryInterval = StrollExpiryTicks * 2;
                    return job;
                }
            }

            if (Rand.Chance(0.5f))
            {
                for (int i = 0; i < FlowerStrollTries; ++i)
                {
                    var cell = lure.Position + GenRadial.RadialPattern[Rand.Range(0, GenRadial.NumCellsInRadius(FlowerStrollRadius))];
                    if (!cell.InBounds(pawn.Map) || !ViviNestSiteEvaluator.IsHoneyFlower(cell.GetPlant(pawn.Map))) { continue; }
                    if (!cell.Standable(pawn.Map) || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some)) { continue; }

                    var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                    job.locomotionUrgency = LocomotionUrgency.Amble;
                    job.expiryInterval = StrollExpiryTicks;
                    return job;
                }
            }

            return null;
        }
    }
}
