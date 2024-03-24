using Verse;

namespace VVRace
{
    public class EnergyRule_Wind : EnergyRule
    {
        public SimpleCurve energyFromWindSpeed;

        public override IntRange ApproximateEnergy => new IntRange((int)energyFromWindSpeed.MinY, (int)energyFromWindSpeed.MaxY);

        public override float CalcEnergy(ArtificialPlant plant, int ticks)
        {
            if (!plant.IsOutside()) { return 0f; }

            return energyFromWindSpeed.Evaluate(plant.Map.windManager.WindSpeed);
        }
    }
}
