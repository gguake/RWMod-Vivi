using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_CompositeSum : ManaFluxRule
    {
        public List<ManaFluxRule> rules;

        public override IntRange FluxRangeForDisplay
        {
            get
            {
                int min = 0, max = 0;
                foreach (var rule in rules)
                {
                    min += rule.FluxRangeForDisplay.min;
                    max += rule.FluxRangeForDisplay.max;
                }

                return new IntRange(min, max);
            }
        }

        public override string GetRuleString()
        {
            var sb = new StringBuilder(rules[0].GetRuleString());
            for (int i = 1; i < rules.Count; ++i)
            {
                sb.AppendInNewLine(rules[i].GetRuleString());
            }

            return sb.ToString();
        }

        public override int CalcManaFlux(Thing thing)
        {
            int sum = 0;
            for (int i = 0; i < rules.Count; ++i)
            {
                sum += rules[i].CalcManaFlux(thing);
            }

            return sum;
        }
    }
}
