using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
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

        public static bool IsOutsideFor(IntVec3 cell, Pawn pawn)
        {
            return pawn == _restrictedPawn && !_restrictedTerritory.InWorkRange(cell);
        }
    }
}
