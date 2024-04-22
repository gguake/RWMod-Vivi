using Verse;

namespace VVRace
{
    public class ManaFluxRule_HasRoof : ManaFluxRule
    {
        public float mana;

        public override IntRange ApproximateManaFlux => new IntRange(0, (int)mana);

        public override float CalcManaFlux(ManaAcceptor plant, int ticks)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0f; }

            if (plant.Position.Roofed(plant.Map))
            {
                return mana / 60000f * ticks;
            }

            return 0f;
        }
    }
}
