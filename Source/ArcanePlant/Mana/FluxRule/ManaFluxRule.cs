using Verse;

namespace VVRace
{
    public abstract class ManaFluxRule
    {
        public abstract IntRange FluxRangeForDisplay { get; }

        public virtual bool IsFluxHidden => false;

        public abstract int CalcManaFlux(Thing thing);

        public abstract string GetRuleString();

        public virtual string GetRuleLabel() => GetRuleString();
    }

}
