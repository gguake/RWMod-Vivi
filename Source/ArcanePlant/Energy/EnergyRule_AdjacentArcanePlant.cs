using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class EnergyRule_AdjacentArcanePlant : EnergyRule
    {
        public float energyByAdjacentArcanePlant;

        public override IntRange ApproximateEnergy => new IntRange(0, (int)energyByAdjacentArcanePlant * 4);

        public override float CalcEnergy(ArcanePlant plant, int ticks)
        {
            if (energyByAdjacentArcanePlant != 0f)
            {
                float energy = 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    if (cell.GetFirstThing<ArcanePlant>(plant.Map) != null)
                    {
                        energy += energyByAdjacentArcanePlant;
                    }
                }

                return energy / 60000f * ticks;
            }

            return 0f;
        }
    }
}
