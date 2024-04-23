using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_AdjacentPollution : ManaFluxRule
    {
        public int manaFromOccupiedPollution;
        public int manaPerAdjacentPollution;

        public override IntRange ApproximateManaFlux => new IntRange(0, manaFromOccupiedPollution + manaPerAdjacentPollution * 4);

        public override string GetRuleString(bool inverse) => 
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_AdjacentPollution_Desc.Translate(
                (inverse ? -manaFromOccupiedPollution : manaFromOccupiedPollution).ToString("+0;-#"),
                (inverse ? -manaPerAdjacentPollution : manaPerAdjacentPollution).ToString("+0;-#"));

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0; }

            var mana = manaFromOccupiedPollution != 0 && plant.Position.IsPolluted(plant.Map) ? Mathf.RoundToInt(manaFromOccupiedPollution) : 0;
            if (manaPerAdjacentPollution != 0)
            {
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    if (cell.IsPolluted(plant.Map))
                    {
                        mana += Mathf.RoundToInt(manaPerAdjacentPollution);
                    }
                }

                return mana;
            }

            return 0;
        }
    }
}
