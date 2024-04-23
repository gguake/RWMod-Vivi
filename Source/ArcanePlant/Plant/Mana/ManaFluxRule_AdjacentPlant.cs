using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_AdjacentPlant : ManaFluxRule
    {
        public int manaPerAdjacentPlant;
        
        public override IntRange ApproximateManaFlux => new IntRange(0, (int)manaPerAdjacentPlant * 4);

        public override string GetRuleString(bool inverse) => 
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_AdjacentPlant_Desc.Translate(
                (inverse ? -manaPerAdjacentPlant : manaPerAdjacentPlant).ToString("+0;-#"));

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0; }

            if (manaPerAdjacentPlant != 0)
            {
                var mana = 0;
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
                            mana += Mathf.RoundToInt(otherPlant.Growth * manaPerAdjacentPlant);
                            break;
                        }
                    }
                }

                return Mathf.RoundToInt(mana);
            }

            return 0;
        }
    }
}
