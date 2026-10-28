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

                // 아포크리파는 모든 거대 말벌의 시조이므로 공격하지 않는다(경호 로드 소속이 아닌 말벌도 마찬가지).
                if (ApocryphaUtility.IsApocrypha(otherPawn))
                {
                    return false;
                }
            }

            // 아포크리파가 피워 낸 멸망꽃·요정꽃도 건드리지 않는다.
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
