using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public abstract class LordToil_ApocryphaHornetGuard : LordToil
    {
        protected const int FeedIntervalTicks = 2500;
        protected const int ReactionCheckIntervalTicks = 30;

        public override bool AllowSatisfyLongNeeds => false;

        protected void KeepFed()
        {
            if (Find.TickManager.TicksGame % FeedIntervalTicks != 0) { return; }

            foreach (var pawn in lord.ownedPawns)
            {
                var food = pawn.needs?.food;
                if (food != null)
                {
                    food.CurLevelPercentage = 1f;
                }
            }
        }

        protected static void TryEngageImmediately(Pawn pawn)
        {
            if (!CanEngage(pawn)) { return; }

            var curJob = pawn.CurJob;
            if (curJob == null || curJob.jobGiver is JobGiver_HornetBerserk) { return; }

            if (pawn.GetLord()?.LordJob is LordJob_ApocryphaHornetGuard guardJob && !guardJob.AnyPotentialTargetNear(pawn)) { return; }

            if (JobGiver_HornetBerserk.TryPrepareAttackJob(pawn))
            {
                pawn.jobs.CheckForJobOverride();
            }
        }

        protected static bool CanEngage(Pawn pawn)
        {
            return pawn.Spawned && !pawn.Downed && pawn.MentalStateDef == VVMentalStateDefOf.VV_HornetBerserk;
        }
    }
}
