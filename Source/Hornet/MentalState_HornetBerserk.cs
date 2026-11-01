using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class MentalState_HornetBerserk : MentalState
    {
        public override bool ForceHostileTo(Thing t)
        {
            if (t is Pawn otherPawn)
            {
                if (otherPawn.kindDef == pawn.kindDef)
                {
                    return false;
                }

                if (ApocryphaUtility.IsApocrypha(otherPawn))
                {
                    return false;
                }
            }

            if (t is Building_ApocryphaFlower)
            {
                return false;
            }

            return true;
        }

        public override bool ForceHostileTo(Faction f)
        {
            return true;
        }

        public override RandomSocialMode SocialModeMax()
        {
            return RandomSocialMode.Off;
        }
    }
}
