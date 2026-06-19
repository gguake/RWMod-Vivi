using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class Need_RoyalJelly : Need
    {
        public const float MinimumThresholdBeRoyal = 0.98f;
        public const float FixedThresholdBeRoyal = 0.995f;

        public bool ShouldBeRoyalIfMature
        {
            get
            {
                var pct = CurLevelPercentage;
                if (pct < MinimumThresholdBeRoyal) { return false; }
                if (pct > FixedThresholdBeRoyal) { return true; }

                var result = false;
                try
                {
                    Rand.PushState(pawn.thingIDNumber);
                    result = Rand.Chance((pct - MinimumThresholdBeRoyal) / (1f - MinimumThresholdBeRoyal));
                }
                finally
                {
                    Rand.PopState();
                }

                return result;
            }
        }

        public Need_RoyalJelly(Pawn pawn) : base(pawn)
        {
            threshPercents = new List<float>() { 0.99f };
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
