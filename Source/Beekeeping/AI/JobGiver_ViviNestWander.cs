using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestWander : JobGiver_Wander
    {
        public JobGiver_ViviNestWander()
        {
            wanderRadius = 5f;
            ticksBetweenWandersRange = new IntRange(120, 360);
            wanderDestValidator = (pawn, cell, root) => pawn.GetNestLordJob()?.Territory.IsRoomCell(cell) ?? true;
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest != null && nest.Territory.IsRoomCell(pawn.Position))
            {
                return pawn.Position;
            }

            return nest != null && nest.TryGetRandomRoomCell(out var cell) ? cell : pawn.Position;
        }
    }
}
