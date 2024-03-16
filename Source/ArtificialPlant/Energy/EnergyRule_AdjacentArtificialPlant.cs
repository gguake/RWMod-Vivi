using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class EnergyRule_AdjacentArtificialPlant : EnergyRule
    {
        public float energyByAdjacentArtificialPlant;

        public override IntRange ApproximateEnergy => new IntRange(0, (int)energyByAdjacentArtificialPlant * 4);

        public override float CalcEnergy(ArtificialPlant plant, int ticks)
        {
            if (energyByAdjacentArtificialPlant != 0f)
            {
                float energy = 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    if (cell.GetFirstThing<ArtificialPlant>(plant.Map) != null)
                    {
                        energy += energyByAdjacentArtificialPlant;
                    }
                }

                return energy / 60000f * ticks;
            }

            return 0f;
        }
    }
}
