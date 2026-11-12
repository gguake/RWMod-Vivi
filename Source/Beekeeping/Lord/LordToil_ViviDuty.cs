using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
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
