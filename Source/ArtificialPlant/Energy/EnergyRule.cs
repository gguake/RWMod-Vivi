using Verse;

namespace VVRace
{
    public abstract class EnergyRule
    {
        public abstract IntRange ApproximateEnergy { get; }

        public abstract float CalcEnergy(ArtificialPlant plant, int ticks);
    }

}
