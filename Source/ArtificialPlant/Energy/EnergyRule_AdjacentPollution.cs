using System.Linq;
using Verse;

namespace VVRace
{
    public class EnergyRule_AdjacentPollution : EnergyRule
    {
        public float energyByOccupiePolluted;
        public float energyByAdjacentPolluted;

        public override IntRange ApproximateEnergy => new IntRange(0, (int)energyByOccupiePolluted + (int)energyByAdjacentPolluted * 4);

        public override float CalcEnergy(ArtificialPlant plant, int ticks)
        {
            if (energyByAdjacentPolluted != 0f)
            {
                var energy = plant.Position.IsPolluted(plant.Map) ? energyByOccupiePolluted : 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    if (cell.IsPolluted(plant.Map))
                    {
                        energy += energyByAdjacentPolluted;
                    }
                }

                return energy / 60000f * ticks;
            }
            else if (energyByOccupiePolluted != 0f)
            {
                return plant.Position.IsPolluted(plant.Map) ? energyByOccupiePolluted / 60000f * ticks : 0f;
            }

            return 0f;
        }
    }
}
