using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_CompositeSum : ManaFluxRule
    {
        public List<ManaFluxRule> rules;

        public override IntRange ApproximateManaFlux
        {
            get
            {
                int min = 0, max = 0;
                foreach (var rule in rules)
                {
                    min += rule.ApproximateManaFlux.min;
                    max += rule.ApproximateManaFlux.max;
                }

                return new IntRange(min, max);
            }
        }

        public override float CalcManaFlux(ManaAcceptor plant, int ticks)
        {
            float sum = 0f;
            for (int i = 0; i < rules.Count; ++i)
            {
                sum += rules[i].CalcManaFlux(plant, ticks);
            }

            return sum;
        }
    }
}
