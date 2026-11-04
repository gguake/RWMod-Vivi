using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class JobGiver_DevourBait : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var lure = (pawn.GetLord()?.LordJob as LordJob_ViviBaitThief)?.Lure;
            if (lure == null || !lure.Spawned || lure.Map != pawn.Map || !lure.HasBait) { return null; }
            if (!pawn.CanReach(lure, PathEndMode.Touch, Danger.Deadly)) { return null; }

            return JobMaker.MakeJob(VVJobDefOf.VV_DevourBait, lure);
        }
    }
}
