using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestNap : ThinkNode_JobGiver
    {
        private static readonly IntRange NapTicks = new IntRange(1250, 2000);
        private const int FallAsleepTicks = 600;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || pawn.needs?.rest == null) { return null; }

            var current = ThinkNode_ViviNestPlay.CurrentPlay(pawn, this);
            if (current != null)
            {
                return pawn.jobs.curDriver.asleep || ThinkNode_ViviNestPlay.Elapsed(current) < FallAsleepTicks ? current : null;
            }

            if (!RestUtility.CanFallAsleep(pawn) || RestUtility.DisturbancePreventsLyingDown(pawn)) { return null; }

            if (!nest.TryFindSleepTarget(pawn, out var target)) { return null; }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ViviNestNap, target);
            job.expiryInterval = NapTicks.RandomInRange;
            job.reportStringOverride = job.def.reportString;
            return job;
        }
    }
}
