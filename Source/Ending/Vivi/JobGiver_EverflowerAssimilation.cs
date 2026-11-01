using Verse;
using Verse.AI;

namespace VVRace
{
    // 〈영원꽃 완전 동화〉 중인 폰에게 고정 작업을 준다. 인간형 생각 트리의 Humanlike_PostMentalState 자리에 끼워 넣는다(ThinkTree_Ending.xml).
    public class JobGiver_EverflowerAssimilation : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!Hediff_EverflowerAssimilation.IsAssimilated(pawn) || !pawn.Spawned) { return null; }

            return JobMaker.MakeJob(EverflowerAssimilationDefOf.VV_EverflowerAssimilationHold);
        }
    }
}
