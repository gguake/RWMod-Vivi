using Verse;

namespace VVRace
{
    public class ManaFluxRule_Constant : ManaFluxRule
    {
        public float mana;

        public override IntRange ApproximateManaFlux => new IntRange((int)mana, (int)mana);

        public override float CalcManaFlux(ArcanePlant plant, int ticks)
        {
            return mana / 60000f * ticks;
        }
    }
}
