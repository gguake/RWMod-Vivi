using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public static class ArcanePlantUtility
    {
        public static IEnumerable<ThingDef> ViviFlowerDefs
        {
            get
            {
                if (_viviFlowerDefs == null)
                {
                    _viviFlowerDefs = new ThingDef[]
                    {
                        VVThingDefOf.VV_Plant_FireArcaneFlower,
                        VVThingDefOf.VV_Plant_IceArcaneFlower,
                        VVThingDefOf.VV_Plant_LightningArcaneFlower,
                        VVThingDefOf.VV_Plant_LifeArcaneFlower
                    };
                }

                return _viviFlowerDefs;
            }
        }
        private static ThingDef[] _viviFlowerDefs;

        public static ManaMapComponent GetManaComponent(this Map map)
        {
            if (map == null) { return null; }

            return map.GetComponent<ManaMapComponent>();
        }

        public static AcceptanceReport CanPlaceArcanePlantToCell(Map map, IntVec3 cell, ThingDef def, Thing currentThing = null)
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

            var arcanePlantPot = map.GetComponent<ArcanePlantMapComponent>()?.GetArcanePlantPot(cell);
            var blockingThings = map.thingGrid.ThingsListAtFast(cell);
            foreach (var thing in blockingThings)
            {
                if (thing == currentThing || thing == arcanePlantPot) { continue; }

                if (thing.def.category == ThingCategory.Building && thing.def.IsEdifice())
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate() + ": " + "BlockedBy".Translate(thing));
                }
            }

            var terrain = cell.GetTerrain(map);
            var isWaterPlant = terrain.IsWater && def.terrainAffordanceNeeded != null && terrain.affordances.Contains(def.terrainAffordanceNeeded);
            if (arcanePlantPot == null && !isWaterPlant && map.fertilityGrid.FertilityAt(cell) <= 0f)
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

                if (thing is Blueprint_Install blueprint_Install && blueprint_Install.ThingToInstall is ArcanePlant)
                {
                    return new AcceptanceReport("IdenticalThingExists".Translate());
                }
            }

            return true;
        }

        public static AcceptanceReport CanLinkEverflower(this Pawn pawn)
        {
            if (!pawn.IsRoyalVivi()) { return false; }

            if (pawn.GetCompVivi().LinkedEverflower != null)
            {
                return new AcceptanceReport(LocalizeString_Gizmo.VV_Gizmo_LinkFailReason_AlreadyLinked.Translate());
            }

            var psychicSensitivity = pawn.GetStatValue(StatDefOf.PsychicSensitivity);
            if (psychicSensitivity < 1.5f)
            {
                return new AcceptanceReport(LocalizeString_Gizmo.VV_Gizmo_LinkFailReason_PsychicSensitivityRequire.Translate());
            }

            return true;
        }

        public static bool TrySpawnViviFlower(Map map, IntVec3 c, out Thing flower, ThingDef flowerDef = null)
        {
            if (flowerDef == null)
            {
                flowerDef = ViviFlowerDefs.RandomElement();
            }

            flower = null;
            if (c.GetPlant(map) != null) { return false; }
            if (c.GetCover(map) != null) { return false; }
            if (c.GetEdifice(map) != null) { return false; }
            if (c.GetFertility(map) < 0.5f) { return false; }
            if (c.GetTemperature(map) < flowerDef.plant.minGrowthTemperature || c.GetTemperature(map) > flowerDef.plant.maxGrowthTemperature) { return false; }
            if (!PlantUtility.SnowAllowsPlanting(c, map)) { return false; }
            if (!PlantUtility.SandAllowsPlanting(c, map)) { return false; }

            return GenSpawn.TrySpawn(flowerDef, c, map, out flower, canWipeEdifices: false);
        }
    }
}
