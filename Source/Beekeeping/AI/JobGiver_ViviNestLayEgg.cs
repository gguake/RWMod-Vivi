using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestLayEgg : JobGiver_LayViviEgg
    {
        public override float GetPriority(Pawn pawn)
        {
            return EggAllowed(pawn) ? base.GetPriority(pawn) : 0f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            return EggAllowed(pawn) ? base.TryGiveJob(pawn) : null;
        }

        private static bool EggAllowed(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            return nest == null || !nest.Settled || nest.Planner.EggAllowed;
        }
    }
}
