using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 할 일이 없을 때 둥지가 지은 크림 벽을 바닐라 벽 다듬기 잡으로 다듬는다(플레이어 지정 없이).
    public class JobGiver_ViviNestSmoothWall : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) { return null; }

            var map = pawn.Map;
            foreach (var cell in nest.Territory.BoundaryCells)
            {
                var wall = cell.GetEdifice(map);
                if (wall == null || wall.Faction != nest.Faction || wall.def.building?.smoothedThing == null) { continue; }
                if (wall.IsBurning() || !pawn.CanReserveAndReach(wall, PathEndMode.Touch, Danger.Some)) { continue; }

                var job = JobMaker.MakeJob(JobDefOf.SmoothWall, wall);
                job.ignoreDesignations = true;
                return job;
            }

            return null;
        }
    }
}
