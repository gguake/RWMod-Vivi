using Verse;

namespace VVRace
{
    public class EnergyRule_Constant : EnergyRule
    {
        public float energy;

        public override IntRange ApproximateEnergy => new IntRange((int)energy, (int)energy);

        public override float CalcEnergy(ArcanePlant plant, int ticks)
        {
            return energy / 60000f * ticks;
        }
    }
}
