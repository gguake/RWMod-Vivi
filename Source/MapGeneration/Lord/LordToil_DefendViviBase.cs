using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordToil_DefendViviBase : LordToil
    {
        public IntVec3 baseCenter;

        public override IntVec3 FlagLoc => baseCenter;

        public LordToil_DefendViviBase(IntVec3 baseCenter)
        {
            this.baseCenter = baseCenter;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                lord.ownedPawns[i].mindState.duty = new PawnDuty(VVDutyDefOf.VV_DefendViviBase, baseCenter);
            }
        }
    }
}
