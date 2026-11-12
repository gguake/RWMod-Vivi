using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestSmoothWall : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) { return null; }

            var map = pawn.Map;
            foreach (var cell in nest.Territory.BoundaryCells)
            {
                var wall = cell.GetEdifice(map);
                if (wall == null || wall.Faction != nest.Faction || wall.def.building?.smoothedThing == null) { continue; }
                if (wall.IsBurning() || !pawn.CanReserve(cell) || !pawn.CanReserveAndReach(wall, PathEndMode.Touch, Danger.Some)) { continue; }

                var job = JobMaker.MakeJob(JobDefOf.SmoothWall, wall);
                job.ignoreDesignations = true;
                return job;
            }

            return null;
        }
    }
}
