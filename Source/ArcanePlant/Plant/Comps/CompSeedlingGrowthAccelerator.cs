using Verse;

namespace VVRace
{
    public class CompProperties_SeedlingGrowthAccelerator : CompProperties
    {
        public float growthBonus;

        public CompProperties_SeedlingGrowthAccelerator()
        {
            compClass = typeof(CompSeedlingGrowthAccelerator);
        }
    }

    public class CompSeedlingGrowthAccelerator : ThingComp
    {
        public CompProperties_SeedlingGrowthAccelerator Props => (CompProperties_SeedlingGrowthAccelerator)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            foreach (var cell in GenAdjFast.AdjacentCellsCardinal(parent.Position))
            {
                if (!cell.InBounds(parent.Map)) { continue; }

                var seedling = cell.GetFirstThing<ArcanePlant_Seedling>(parent.Map);
                if (seedling != null)
                {
                    seedling.ResetGrowthBonusCache();
                }
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.WillReplace)
            {
                foreach (var cell in GenAdjFast.AdjacentCellsCardinal(parent.Position))
                {
                    if (!cell.InBounds(map)) { continue; }

                    var seedling = cell.GetFirstThing<ArcanePlant_Seedling>(map);
                    seedling.ResetGrowthBonusCache();
                }
            }
        }
    }
}
