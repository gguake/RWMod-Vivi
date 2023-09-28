using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class EnergyRule_AdjacentPlant : EnergyRule
    {
        public float energyByAdjacentPlant;

        public override IntRange ApproximateEnergy => new IntRange(0, (int)energyByAdjacentPlant * 4);

        public override float CalcEnergy(ArtificialPlant plant, int ticks)
        {
            if (energyByAdjacentPlant != 0f)
            {
                float energy = 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    if (cell.GetFirstThing<Plant>(plant.Map) != null || cell.GetFirstThing<ArtificialPlant>(plant.Map) != null)
                    {
                        energy += energyByAdjacentPlant;
                    }
                }

                return energy / 60000f * ticks;
            }

            return 0f;
        }
    }
}
