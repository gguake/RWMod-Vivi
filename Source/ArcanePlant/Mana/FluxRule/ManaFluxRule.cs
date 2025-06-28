using Verse;

namespace VVRace
{
    public abstract class ManaFluxRule
    {
        public abstract IntRange FluxRangeForDisplay { get; }

        public abstract int CalcManaFlux(Thing thing);

        public abstract string GetRuleString();
    }

}
