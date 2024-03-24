using Verse;

namespace VVRace
{
    public class ManaFluxRule_HasRoof : ManaFluxRule
    {
        public float mana;

        public override IntRange ApproximateManaFlux => new IntRange(0, (int)mana);

        public override float CalcManaFlux(ArcanePlant plant, int ticks)
        {
            if (plant.Position.Roofed(plant.Map))
            {
                return mana / 60000f * ticks;
            }

            return 0f;
        }
    }
}
