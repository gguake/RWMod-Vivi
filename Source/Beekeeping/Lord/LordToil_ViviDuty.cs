using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 모든 구성원에게 같은 듀티를 주는 단순 토일(정찰 둘러보기·미끼 도둑 등).
    public class LordToil_ViviDuty : LordToil
    {
        private readonly DutyDef _duty;
        private readonly IntVec3 _focus;

        public override IntVec3 FlagLoc => _focus;

        public LordToil_ViviDuty(DutyDef duty, IntVec3 focus)
        {
            _duty = duty;
            _focus = focus;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; ++i)
            {
                lord.ownedPawns[i].mindState.duty = new PawnDuty(_duty, _focus);
            }
        }
    }
}
