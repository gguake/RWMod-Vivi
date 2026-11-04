using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 둥지를 떠날 때 어른이 둥지 아기를 바닐라 데려가기 잡(같은 세력이면 구조로 보인다)으로 안고 맵 밖으로 나간다.
    // 같은 세력 폰을 안고 나가면 바닐라가 안긴 폰도 함께 맵을 나가게 한다.
    public class JobGiver_ViviNestCarryBabyOut : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.DevelopmentalStage.Adult() || pawn.carryTracker?.CarriedThing != null) { return null; }

            var lord = pawn.GetLord();
            if (lord == null) { return null; }

            for (int i = 0; i < lord.ownedPawns.Count; ++i)
            {
                var baby = lord.ownedPawns[i];
                if (!baby.Spawned || !baby.DevelopmentalStage.Baby() || baby.Map != pawn.Map) { continue; }
                if (!pawn.CanReserveAndReach(baby, PathEndMode.Touch, Danger.Deadly)) { continue; }
                if (!RCellFinder.TryFindBestExitSpot(pawn, out var exit, TraverseMode.ByPawn)) { return null; }

                var job = JobMaker.MakeJob(JobDefOf.Kidnap, baby, exit);
                job.count = 1;
                return job;
            }

            return null;
        }
    }
}
