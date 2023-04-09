using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Need_RoyalJelly : Need
    {
        public const float ThresholdBeRoyal = 0.999f;

        public bool ShouldBeRoyalIfMature
        {
            get
            {
                return CurLevelPercentage >= ThresholdBeRoyal;
            }
        }

        public Need_RoyalJelly(Pawn pawn) : base(pawn)
        {
            threshPercents = new List<float>() { ThresholdBeRoyal };
        }

        public override void SetInitialLevel()
        {
            CurLevelPercentage = 0f;
        }

        public override void NeedInterval()
        {
        }
    }
}
