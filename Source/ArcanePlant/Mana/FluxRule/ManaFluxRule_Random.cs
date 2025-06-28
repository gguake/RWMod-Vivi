using Verse;

namespace VVRace
{
    public class ManaFluxRule_Random : ManaFluxRule
    {
        public int min;
        public int max;

        public override IntRange FluxRangeForDisplay => new IntRange(-1, -1);

        public override string GetRuleString() => $"?";

        public override int CalcManaFlux(Thing thing)
        {
            return min + Rand.Int % (max - min);
        }
    }
}
