using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_WanderForEscort : JobGiver_Wander
    {
        public JobGiver_WanderForEscort()
        {
            wanderRadius = 3f;
            ticksBetweenWandersRange = new IntRange(125, 200);
        }

        private GlobalTargetInfo Target(Pawn pawn)
        {
            return pawn.GetMindLinkMaster();
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var globalTargetInfo = Target(pawn);
            if (globalTargetInfo.Map != pawn.Map)
            {
                return null;
            }

            var job = base.TryGiveJob(pawn);
            job.reportStringOverride = "Escorting".Translate(globalTargetInfo.Thing.Named("TARGET"));
            return job;
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            return Target(pawn).Cell;
        }

        protected override void DecorateGotoJob(Job job)
        {
            job.expiryInterval = 120;
            job.expireRequiresEnemiesNearby = true;
        }
    }
}
