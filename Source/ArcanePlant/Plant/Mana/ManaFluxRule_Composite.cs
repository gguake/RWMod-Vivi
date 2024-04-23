using System.Collections.Generic;
using System.Text;
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

        public override string GetRuleString(bool inverse)
        {
            var sb = new StringBuilder(rules[0].GetRuleString(inverse));
            for (int i = 1; i < rules.Count; ++i)
            {
                sb.AppendInNewLine(rules[i].GetRuleString(inverse));
            }

            return sb.ToString();
        }

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            int sum = 0;
            for (int i = 0; i < rules.Count; ++i)
            {
                sum += rules[i].CalcManaFlux(plant);
            }

            return sum;
        }
    }
}
