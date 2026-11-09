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
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) { return null; }

            var map = pawn.Map;
            foreach (var cell in nest.Territory.BoundaryCells)
            {
                var wall = cell.GetEdifice(map);
                if (wall == null || wall.Faction != nest.Faction || wall.def.building?.smoothedThing == null) { continue; }
                // 바닐라 벽 다듬기 잡은 벽과 그 칸을 함께 예약하므로(JobDriver_SmoothWall) 바닐라 워크기버처럼 칸 예약도 미리 본다.
                // 칸만 다른 폰이 예약해 둔 벽(서서 먹을 자리처럼 칸을 예약하는 잡이 있다)을 그대로 주면 바닐라 잡이 예약 실패 오류를 낸다(자동 테스트 p3 B2, 다시 지은 벽).
                if (wall.IsBurning() || !pawn.CanReserve(cell) || !pawn.CanReserveAndReach(wall, PathEndMode.Touch, Danger.Some)) { continue; }

                var job = JobMaker.MakeJob(JobDefOf.SmoothWall, wall);
                job.ignoreDesignations = true;
                return job;
            }

            return null;
        }
    }
}
