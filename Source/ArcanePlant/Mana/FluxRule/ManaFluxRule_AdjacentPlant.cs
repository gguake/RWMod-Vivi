using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_AdjacentPlant : ManaFluxRule
    {
        public int manaPerAdjacentPlant;
        
        public override IntRange FluxRangeForDisplay => new IntRange(0, (int)manaPerAdjacentPlant * 4);

        public override string GetRuleString() => 
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_AdjacentPlant_Desc.Translate(
                manaPerAdjacentPlant.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            if (manaPerAdjacentPlant != 0)
            {
                var mana = 0;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(thing).Where(v => v.InBounds(thing.Map)))
                {
                    foreach (var t in cell.GetThingList(thing.Map))
                    {
                        if (t is ArcanePlant)
                        {
                            mana += manaPerAdjacentPlant;
                            break;
                        }
                        else if (t is Plant otherPlant)
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
