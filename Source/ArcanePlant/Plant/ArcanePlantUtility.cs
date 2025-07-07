using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public static class ArcanePlantUtility
    {
        public static AcceptanceReport CanPlaceArcanePlantToCell(Map map, IntVec3 cell, ThingDef def)
        {
            if (map == null || def == null || !cell.InBounds(map)) { return false; }

            if (cell.InNoBuildEdgeArea(map))
            {
                return new AcceptanceReport("TooCloseToMapEdge".Translate());
            }

            if (cell.Fogged(map))
            {
                return new AcceptanceReport("CannotPlaceInUndiscovered".Translate());
            }

            var blockingThings = map.thingGrid.ThingsListAtFast(cell);
            var onArcanePlantPot = blockingThings.Any(v => v is ArcanePlantPot);

            foreach (var thing in blockingThings)
            {
                if (thing.def.category == ThingCategory.Building && !(thing is ArcanePlantPot) && thing.def.IsEdifice())
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate() + ": " + "BlockedBy".Translate(thing));
                }
            }

            var terrain = cell.GetTerrain(map);
            var isWaterPlant = terrain.IsWater && def.terrainAffordanceNeeded != null && terrain.affordances.Contains(def.terrainAffordanceNeeded);
            if (!onArcanePlantPot && !isWaterPlant && map.fertilityGrid.FertilityAt(cell) <= 0f)
            {
                return new AcceptanceReport("CannotBePlantedHere".Translate() + ": " + "MessageWarningNotEnoughFertility".Translate().CapitalizeFirst());
            }

            foreach (Thing thing in cell.GetThingList(map))
            {
                if (map.designationManager.DesignationOn(thing, DesignationDefOf.Uninstall) != null)
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate());
                }

                if (map.designationManager.DesignationOn(thing, DesignationDefOf.Deconstruct) != null)
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate());
                }

                if (thing is Building building && map.listerBuildings.TryGetReinstallBlueprint(building, out var _))
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate());
                }

                if (thing is Blueprint_PlantSeed blueprint_PlantSeed)
                {
                    return new AcceptanceReport("IdenticalThingExists".Translate());
                }

                if (thing is Blueprint_Install blueprint_Install && blueprint_Install.ThingToInstall is ArcanePlant)
                {
                    return new AcceptanceReport("IdenticalThingExists".Translate());
                }
            }

            return true;
        }
    }
}
