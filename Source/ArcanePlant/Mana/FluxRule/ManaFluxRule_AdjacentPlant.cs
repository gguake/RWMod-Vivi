using RimWorld;
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

        public override string GetRuleLabel() =>
            LocalizeString_Stat.VV_ManaFluxRuleLabel_AdjacentPlant.Translate();

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            if (manaPerAdjacentPlant != 0)
            {
                var map = thing.Map;
                var mana = 0;
                var size = thing.def.size;
                if (size.x == 1 && size.z == 1)
                {
                    var pos = thing.Position;
                    for (int i = 0; i < 4; i++)
                    {
                        var cell = pos + GenAdj.CardinalDirections[i];
                        if (!cell.InBounds(map)) { continue; }
                        mana += CalcPlantManaAt(cell, map);
                    }
                }
                else
                {
                    foreach (var cell in GenAdj.CellsAdjacentCardinal(thing))
                    {
                        if (!cell.InBounds(map)) { continue; }
                        mana += CalcPlantManaAt(cell, map);
                    }
                }

                return mana;
            }

            return 0;
        }

        private int CalcPlantManaAt(IntVec3 cell, Map map)
        {
            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (t is ArcanePlant)
                {
                    return manaPerAdjacentPlant;
                }
                else if (t is Plant otherPlant)
                {
                    return Mathf.RoundToInt(otherPlant.Growth * manaPerAdjacentPlant);
                }
            }
            return 0;
        }
    }
}
