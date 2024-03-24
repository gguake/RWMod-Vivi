using Verse;

namespace VVRace
{
    public class EnergyRule_HasRoof : EnergyRule
    {
        public float energy;

        public override IntRange ApproximateEnergy => new IntRange(0, (int)energy);

        public override float CalcEnergy(ArcanePlant plant, int ticks)
        {
            if (plant.Position.Roofed(plant.Map))
            {
                return energy / 60000f * ticks;
            }

            return 0f;
        }
    }
}
