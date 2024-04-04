using Verse;

namespace VVRace
{
    public abstract class ManaFluxRule
    {
        public abstract IntRange ApproximateManaFlux { get; }

        public abstract float CalcManaFlux(ManaAcceptor manaAcceptor, int ticks);
    }

}
