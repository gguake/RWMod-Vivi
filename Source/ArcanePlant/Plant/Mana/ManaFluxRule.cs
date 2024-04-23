using Verse;

namespace VVRace
{
    public abstract class ManaFluxRule
    {
        public abstract IntRange ApproximateManaFlux { get; }

        public abstract int CalcManaFlux(ManaAcceptor manaAcceptor);

        public abstract string GetRuleString(bool inverse);
    }

}
