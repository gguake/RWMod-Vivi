using System.Linq;
using Verse;

namespace VVRace
{
    public static class ArtificialPlantUtility
    {
        public static bool CanPlaceArtificialPlantToCell(Map map, IntVec3 cell, ThingDef def)
        {
            if (map == null || def == null || !cell.InBounds(map)) { return false; }

            var blockingThings = map.thingGrid.ThingsListAtFast(cell);
            var onArtificialPlantPot = blockingThings.Any(v => v is ArtificialPlantPot);

            var terrain = cell.GetTerrain(map);
            var isWaterPlant = terrain.IsWater && def.terrainAffordanceNeeded != null && terrain.affordances.Contains(def.terrainAffordanceNeeded);
            if (!onArtificialPlantPot && !isWaterPlant && map.fertilityGrid.FertilityAt(cell) <= 0f)
            {
                return false;
            }

            return true;
        }

    }
}
