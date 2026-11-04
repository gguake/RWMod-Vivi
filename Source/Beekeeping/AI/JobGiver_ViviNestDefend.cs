using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 무장한 둥지 비비만 바닐라 지점 방어로 둥지(듀티 지점) 가까이 온 적·사냥하는 포식자와 싸운다.
    public class JobGiver_ViviNestDefend : JobGiver_AIDefendPoint
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.GetNestLordJob() == null || pawn.equipment?.Primary == null || pawn.WorkTagIsDisabled(WorkTags.Violent)) { return null; }

            return base.TryGiveJob(pawn);
        }
    }
}
