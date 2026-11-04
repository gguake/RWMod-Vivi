using Verse;
using Verse.AI;

namespace VVRace
{
    // 할 일이 없으면 둥지 방 안을 거닌다(바닐라 산책에 기준점과 도착 칸 검사만 바꾼다).
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
