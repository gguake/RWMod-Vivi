using System.Linq;
using Verse;

namespace VVRace
{
    public static class ArcanePlantUtility
    {
        public static bool CanPlaceArcanePlantToCell(Map map, IntVec3 cell, ThingDef def)
        {
            if (map == null || def == null || !cell.InBounds(map)) { return false; }

            var blockingThings = map.thingGrid.ThingsListAtFast(cell);
            var onArcanePlantPot = blockingThings.Any(v => v is ArcanePlantPot);

            var terrain = cell.GetTerrain(map);
            var isWaterPlant = terrain.IsWater && def.terrainAffordanceNeeded != null && terrain.affordances.Contains(def.terrainAffordanceNeeded);
            if (!onArcanePlantPot && !isWaterPlant && map.fertilityGrid.FertilityAt(cell) <= 0f)
            {
                return false;
            }

            return true;
        }

    }
}
