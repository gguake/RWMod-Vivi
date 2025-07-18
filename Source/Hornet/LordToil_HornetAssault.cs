using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordToil_HornetAssault : LordToil
    {
        public override void UpdateAllDuties()
        {
            foreach (var pawn in lord.ownedPawns)
            {
                pawn.mindState.duty = new PawnDuty(VVDutyDefOf.VV_TitanicHornetAssault);
            }
        }
    }
}
