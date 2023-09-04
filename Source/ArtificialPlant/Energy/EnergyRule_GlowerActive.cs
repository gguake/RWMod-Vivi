using Verse;

namespace VVRace
{
    public class EnergyRule_GlowerActive : EnergyRule
    {
        public float energy;

        public override IntRange ApproximateEnergy => new IntRange(0, (int)energy);

        public override float CalcEnergy(ArtificialPlant plant, int ticks)
        {
            var compGlower = plant.TryGetComp<CompGlowerFloraEnergy>();
            if (compGlower.Glows)
            {
                return energy / 60000f * ticks;
            }

            return 0f;
        }
    }
}
