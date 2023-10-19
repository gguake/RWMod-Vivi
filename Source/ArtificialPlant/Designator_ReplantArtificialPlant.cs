using RimWorld;
using Verse;

namespace VVRace
{
    public class Designator_ReplantArtificialPlant : Designator_Install
    {
        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map))
            {
                return false;
            }
            if (c.InNoBuildEdgeArea(Map))
            {
                return new AcceptanceReport("TooCloseToMapEdge".Translate());
            }
            if (c.Fogged(Map))
            {
                return new AcceptanceReport("CannotPlaceInUndiscovered".Translate());
            }

            var blockingThings = Map.thingGrid.ThingsListAtFast(c);
            var onArtificialPlantPot = blockingThings.Any(v => v is ArtificialPlantPot);

            foreach (var thing in blockingThings)
            {
                if (thing.def.category == ThingCategory.Building && !(thing is ArtificialPlantPot) && thing.def.IsEdifice())
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate() + ": " + "BlockedBy".Translate(thing));
                }
            }

            var terrain = c.GetTerrain(Map);
            var isWaterPlant = terrain.IsWater && ThingToInstall.def.terrainAffordanceNeeded != null && terrain.affordances.Contains(ThingToInstall.def.terrainAffordanceNeeded);
            if (!onArtificialPlantPot && !isWaterPlant && Map.fertilityGrid.FertilityAt(c) <= 0f)
            {
                return new AcceptanceReport("CannotBePlantedHere".Translate() + ": " + "MessageWarningNotEnoughFertility".Translate().CapitalizeFirst());
            }

            foreach (Thing thing in c.GetThingList(Map))
            {
                if (Map.designationManager.DesignationOn(thing, DesignationDefOf.Uninstall) != null)
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate());
                }

                if (Map.designationManager.DesignationOn(thing, DesignationDefOf.Deconstruct) != null)
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate());
                }

                if (thing is Building building && Map.listerBuildings.TryGetReinstallBlueprint(building, out var _))
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate());
                }

                if (thing is Blueprint_Install blueprint_Install && blueprint_Install.ThingToInstall is ArtificialPlant)
                {
                    return new AcceptanceReport("IdenticalThingExists".Translate());
                }
            }

            return base.CanDesignateCell(c);
        }
    }
}
