using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 로열의 알 낳기: 기존 알 낳기 잡 기버(둥지 부화기에 낳는다)를 그대로 쓰되, 둥지가 새 구성원을 받을 수 있을 때만 낳는다
    // (둥지 식량이 넉넉하고 새 알까지 방 수용량 안이거나 증축할 자리가 있을 때, ViviNestPlanner.EggAllowed). 못 낳는 동안 알 게이지는 찬 채 기다린다.
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
