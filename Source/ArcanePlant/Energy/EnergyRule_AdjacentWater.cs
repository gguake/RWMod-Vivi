using System.Linq;
using Verse;

namespace VVRace
{
    public class EnergyRule_AdjacentWater : EnergyRule
    {
        public float energyByOccupiedWaterCell;
        public float energyByAdjacentWaterCell;

        public override IntRange ApproximateEnergy => new IntRange(0, (int)energyByOccupiedWaterCell + (int)energyByAdjacentWaterCell * 4);

        public override float CalcEnergy(ArcanePlant plant, int ticks)
        {
            if (energyByAdjacentWaterCell != 0f)
            {
                var energy = plant.Position.GetTerrain(plant.Map).IsWater ? energyByOccupiedWaterCell : 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    var terrain = cell.GetTerrain(plant.Map);
                    if (terrain.IsWater && !terrain.bridge)
                    {
                        energy += energyByAdjacentWaterCell;
                    }
                }

                return energy / 60000f * ticks;
            }
            else if (energyByOccupiedWaterCell != 0f)
            {
                return plant.Position.GetTerrain(plant.Map).IsWater ? energyByOccupiedWaterCell / 60000f * ticks : 0f;
            }

            return 0f;
        }
    }
}
