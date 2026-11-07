using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 생활 듀티의 일 묶음: 그 안에서 일을 고르는 동안 둥지 활동 범위(ViviNestTerritory가 칠한 칸) 밖 칸과 그 칸의 물건을 금지로 본다.
    // 바닐라 ThinkNode_ForbidOutsideFlagRadius(듀티 지점 한 점에서의 반경)를 칠한 칸으로 바꾼 것이다. 바닐라처럼 일을 고르는 동안만 묶고(하던 일은 끊지 않는다),
    // 판정은 ForbidUtility.IsForbidden(IntVec3, Pawn) 패치(BeekeepingPatch)가 한다.
    public class ThinkNode_ViviNestForbidOutsideActivityArea : ThinkNode_Priority
    {
        private static Pawn _restrictedPawn;
        private static ViviNestTerritory _restrictedTerritory;

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null) { return base.TryIssueJobPackage(pawn, jobParams); }

            var prevPawn = _restrictedPawn;
            var prevTerritory = _restrictedTerritory;
            _restrictedPawn = pawn;
            _restrictedTerritory = nest.Territory;
            try
            {
                return base.TryIssueJobPackage(pawn, jobParams);
            }
            finally
            {
                _restrictedPawn = prevPawn;
                _restrictedTerritory = prevTerritory;
            }
        }

        // 지금 이 묶음에서 일을 고르는 둥지 비비에게 그 칸이 활동 범위 밖인지
        public static bool IsOutsideFor(IntVec3 cell, Pawn pawn)
        {
            return pawn == _restrictedPawn && !_restrictedTerritory.InWorkRange(cell);
        }
    }
}
