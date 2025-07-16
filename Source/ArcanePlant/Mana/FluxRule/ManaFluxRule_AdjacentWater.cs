using System.Linq;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_AdjacentWater : ManaFluxRule
    {
        public int manaFromOccupiedWater;
        public int manaPerAdjacentWater;

        public override IntRange FluxRangeForDisplay => new IntRange(0, manaFromOccupiedWater + manaPerAdjacentWater * 4);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_AdjacentWater_Desc.Translate(
                manaFromOccupiedWater.ToString("+0;-#"),
                manaPerAdjacentWater.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            if (manaPerAdjacentWater != 0)
            {
                var terrain = thing.Position.GetTerrain(thing.Map);
                var mana = terrain.IsWater && !terrain.bridge ? manaFromOccupiedWater : 0;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(thing).Where(v => v.InBounds(thing.Map)))
                {
                    terrain = cell.GetTerrain(thing.Map);
                    if (terrain.IsWater && !terrain.bridge)
                    {
                        mana += manaPerAdjacentWater;
                    }
                }

                return mana;
            }
            else if (manaFromOccupiedWater != 0)
            {
                return thing.Position.GetTerrain(thing.Map).IsWater ? manaFromOccupiedWater : 0;
            }

            return 0;
        }
    }
}
