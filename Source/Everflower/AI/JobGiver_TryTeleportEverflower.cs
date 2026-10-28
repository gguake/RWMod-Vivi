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
            if (compVivi != null && compVivi.isRoyal && compVivi.LinkedEverflower != null)
            {
                if (compVivi.LinkedEverflower.EverflowerComp.IsEnshrined) { return 0; }

                return compVivi.LinkedEverflower.ReservedTeleportCell.HasValue ? 10 : 0;
            }

            return 0;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var compVivi = pawn.GetCompVivi();
            if (compVivi == null || !compVivi.isRoyal) { return null; }

            var targetEverflower = compVivi.LinkedEverflower;
            if (targetEverflower == null) { return null; }

            if (!targetEverflower.ReservedTeleportCell.HasValue) { return null; }

            // 제단에 안치된 영원꽃은 다시 옮기지 않는다.
            if (targetEverflower.EverflowerComp.IsEnshrined) { return null; }

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
