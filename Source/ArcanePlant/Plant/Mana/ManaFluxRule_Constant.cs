using Verse;

namespace VVRace
{
    public class ManaFluxRule_Constant : ManaFluxRule
    {
        public float mana;

        public override IntRange ApproximateManaFlux => new IntRange((int)mana, (int)mana);

        public override float CalcManaFlux(ManaAcceptor plant, int ticks)
        {
            return mana / 60000f * ticks;
        }
    }
}
