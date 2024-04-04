using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_AdjacentPlant : ManaFluxRule
    {
        public float manaPerAdjacentPlant;
        
        public override IntRange ApproximateManaFlux => new IntRange(0, (int)manaPerAdjacentPlant * 4);

        public override float CalcManaFlux(ManaAcceptor plant, int ticks)
        {
            if (manaPerAdjacentPlant != 0f)
            {
                var mana = 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    if (cell.GetFirstThing<Plant>(plant.Map) != null || cell.GetFirstThing<ArcanePlant>(plant.Map) != null)
                    {
                        mana += manaPerAdjacentPlant;
                    }
                }

                return mana / 60000f * ticks;
            }

            return 0f;
        }
    }
}
