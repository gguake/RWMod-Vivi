using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class EnergyRule_CompositeSum : EnergyRule
    {
        public List<EnergyRule> rules;

        public override IntRange ApproximateEnergy
        {
            get
            {
                int min = 0, max = 0;
                foreach (var rule in rules)
                {
                    min += rule.ApproximateEnergy.min;
                    max += rule.ApproximateEnergy.max;
                }

                return new IntRange(min, max);
            }
        }

        public override float CalcEnergy(ArtificialPlant plant, int ticks)
        {
            float sum = 0f;
            for (int i = 0; i < rules.Count; ++i)
            {
                sum += rules[i].CalcEnergy(plant, ticks);
            }

            return sum;
        }
    }
}
