using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_Everflower : ManaFluxRule
    {
        public List<IntRange> ranges;

        public override IntRange FluxRangeForDisplay => new IntRange(-1, -1);

        public override string GetRuleString() => $"+???";

        public override int CalcManaFlux(Thing thing)
        {
            if (thing is ArcanePlant_Everflower everflower && everflower.EverflowerComp != null)
            {
                var level = everflower.EverflowerComp.AttunementLevel;
                return ranges[Mathf.Min(ranges.Count - 1, level)].RandomInRange;
            }

            return 0;
        }
    }
}
