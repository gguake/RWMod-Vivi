using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_TryTeleportEverflower : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            var compVivi = pawn.GetCompVivi();
            return compVivi != null && compVivi.isRoyal && compVivi.LinkedEverflower.ReservedTeleportCell.HasValue ? 10 : 0;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var compVivi = pawn.GetCompVivi();
            if (compVivi == null || !compVivi.isRoyal) { return null; }

            var targetEverflower = compVivi.LinkedEverflower;
            if (targetEverflower == null) { return null; }

            if (!targetEverflower.ReservedTeleportCell.HasValue) { return null; }

            if (targetEverflower.IsForbidden(pawn)) { return null; }
            if (targetEverflower.IsRitualTarget()) { return null; }

            if (pawn.InMentalState) { return null; }
            if (pawn.Downed) { return null; }

            if (!pawn.CanReserveAndReach(targetEverflower, PathEndMode.Touch, Danger.Deadly)) { return null; }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_MoveEverflower, targetEverflower);
            return job;
        }
    }
}
