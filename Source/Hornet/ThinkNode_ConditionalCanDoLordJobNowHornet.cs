using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class ThinkNode_ConditionalCanDoLordJobNowHornet : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn.IsBurning())
            {
                return false;
            }
            if (pawn.Drafted)
            {
                return false;
            }
            if (!pawn.Awake())
            {
                return false;
            }
            if (pawn.Downed && !pawn.DutyActiveWhenDown())
            {
                return false;
            }
            if (pawn.GetLord() == null)
            {
                return false;
            }
            if (!pawn.GetLord().CurLordToil.AssignsDuties)
            { 
                return false;
            }

            Log.Message($"{123} {pawn.mindState.duty} {pawn.GetLord()}");
            return true;
        }
    }
}
