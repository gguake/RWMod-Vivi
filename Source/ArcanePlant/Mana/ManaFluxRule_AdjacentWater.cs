using System.Linq;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_AdjacentWater : ManaFluxRule
    {
        public float manaFromOccupiedWater;
        public float manaPerAdjacentWater;

        public override IntRange ApproximateManaFlux => new IntRange(0, (int)manaFromOccupiedWater + (int)manaPerAdjacentWater * 4);

        public override float CalcManaFlux(ArcanePlant plant, int ticks)
        {
            if (manaPerAdjacentWater != 0f)
            {
                var mana = plant.Position.GetTerrain(plant.Map).IsWater ? manaFromOccupiedWater : 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    var terrain = cell.GetTerrain(plant.Map);
                    if (terrain.IsWater && !terrain.bridge)
                    {
                        mana += manaPerAdjacentWater;
                    }
                }

                return mana / 60000f * ticks;
            }
            else if (manaFromOccupiedWater != 0f)
            {
                return plant.Position.GetTerrain(plant.Map).IsWater ? manaFromOccupiedWater / 60000f * ticks : 0f;
            }

            return 0f;
        }
    }
}
