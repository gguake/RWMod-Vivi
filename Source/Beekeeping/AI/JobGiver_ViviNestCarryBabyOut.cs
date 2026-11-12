using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
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
