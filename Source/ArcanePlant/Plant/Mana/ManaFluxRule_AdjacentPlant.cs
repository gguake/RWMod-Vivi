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
            if (!plant.Spawned || plant.Destroyed) { return 0f; }

            if (manaPerAdjacentPlant != 0f)
            {
                var mana = 0f;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    foreach (var thing in cell.GetThingList(plant.Map))
                    {
                        if (thing is ArcanePlant)
                        {
                            mana += manaPerAdjacentPlant;
                            break;
                        }
                        else if (thing is Plant otherPlant)
                        {
                            mana += otherPlant.Growth * manaPerAdjacentPlant;
                            break;
                        }
                    }
                }

                return mana / 60000f * ticks;
            }

            return 0f;
        }
    }
}
