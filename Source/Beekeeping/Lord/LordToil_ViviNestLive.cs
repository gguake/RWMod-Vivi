using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordToil_ViviNestLive : LordToil
    {
        private readonly IntVec3 _nestCell;

        public override IntVec3 FlagLoc => _nestCell;

        public override bool AllowSatisfyLongNeeds => true;

        public LordToil_ViviNestLive(IntVec3 nestCell)
        {
            _nestCell = nestCell;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; ++i)
            {
                lord.ownedPawns[i].mindState.duty = new PawnDuty(VVDutyDefOf.VV_ViviNestLife, _nestCell);
            }
        }
    }
}
