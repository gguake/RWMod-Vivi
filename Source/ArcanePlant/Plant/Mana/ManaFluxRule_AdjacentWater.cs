using System.Linq;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_AdjacentWater : ManaFluxRule
    {
        public int manaFromOccupiedWater;
        public int manaPerAdjacentWater;

        public override IntRange ApproximateManaFlux => new IntRange(0, manaFromOccupiedWater + manaPerAdjacentWater * 4);

        public override string GetRuleString(bool inverse) =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_AdjacentWater_Desc.Translate(
                inverse ? -manaFromOccupiedWater : manaFromOccupiedWater,
                inverse ? -manaPerAdjacentWater : manaPerAdjacentWater);

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0; }

            if (manaPerAdjacentWater != 0)
            {
                var mana = plant.Position.GetTerrain(plant.Map).IsWater ? manaFromOccupiedWater : 0;
                foreach (var cell in GenAdj.CellsAdjacentCardinal(plant).Where(v => v.InBounds(plant.Map)))
                {
                    var terrain = cell.GetTerrain(plant.Map);
                    if (terrain.IsWater && !terrain.bridge)
                    {
                        mana += manaPerAdjacentWater;
                    }
                }

                return mana;
            }
            else if (manaFromOccupiedWater != 0)
            {
                return plant.Position.GetTerrain(plant.Map).IsWater ? manaFromOccupiedWater : 0;
            }

            return 0;
        }
    }
}
