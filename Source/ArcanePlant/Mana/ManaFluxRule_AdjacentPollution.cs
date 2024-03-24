using System.Linq;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_AdjacentPollution : ManaFluxRule
    {
        public float manaFromOccupiedPollution;
        public float manaPerAdjacentPollution;

        public override IntRange ApproximateManaFlux => new IntRange(0, (int)manaFromOccupiedPollution + (int)manaPerAdjacentPollution * 4);

        public override float CalcManaFlux(ArcanePlant plant, int ticks)
        {
            if (manaPerAdjacentPollution != 0f)
            {
                var mana = plant.Position.IsPolluted(plant.Map) ? manaFromOccupiedPollution : 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    if (cell.IsPolluted(plant.Map))
                    {
                        mana += manaPerAdjacentPollution;
                    }
                }

                return mana / 60000f * ticks;
            }
            else if (manaFromOccupiedPollution != 0f)
            {
                return plant.Position.IsPolluted(plant.Map) ? manaFromOccupiedPollution / 60000f * ticks : 0f;
            }

            return 0f;
        }
    }
}
