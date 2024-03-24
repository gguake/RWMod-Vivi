using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_AdjacentArcanePlant : ManaFluxRule
    {
        public float manaPerAdjacentArcanePlant;

        public override IntRange ApproximateManaFlux => new IntRange(0, (int)manaPerAdjacentArcanePlant * 4);

        public override float CalcManaFlux(ArcanePlant plant, int ticks)
        {
            if (manaPerAdjacentArcanePlant != 0f)
            {
                var mana = 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    if (cell.GetFirstThing<ArcanePlant>(plant.Map) != null)
                    {
                        mana += manaPerAdjacentArcanePlant;
                    }
                }

                return mana / 60000f * ticks;
            }

            return 0f;
        }
    }
}
