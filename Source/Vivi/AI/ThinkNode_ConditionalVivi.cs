using Verse;
using Verse.AI;

namespace VVRace
{
    public class ThinkNode_ConditionalVivi : ThinkNode_Conditional
    {
        public override float GetPriority(Pawn pawn)
        {
            if (!(pawn is Vivi vivi))
            {
                return 0f;
            }

            return 10f;
        }

        protected override bool Satisfied(Pawn pawn)
        {
            return pawn is Vivi;
        }
    }
}
