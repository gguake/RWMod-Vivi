using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Designator_ReplantArcanePlant : Designator_Install
    {
        protected new Thing MiniToInstallOrBuildingToReinstall
        {
            get
            {
                var objects = Find.Selector.SelectedObjects;
                if (objects.NullOrEmpty())
                {
                    return null;
                }
                if (objects.Count == 1)
                {
                    if (objects[0] is MinifiedArcanePlant minified)
                    {
                        return minified;
                    }
                    else if (objects[0] is ArcanePlant plant && plant.def.Minifiable)
                    {
                        return plant;
                    }
                }
                else
                {
                    if (!(objects[0] is Thing firstThing)) { return null; }

                    var firstDef = firstThing.GetInnerIfMinified().def;

                    for (int i = 1; i < objects.Count; ++i)
                    {
                        var thing = objects[i] as Thing;
                        if (thing.GetInnerIfMinified().def != firstDef) { return null; }
                    }

                    for (int i = 0; i < objects.Count; ++i)
                    {
                        var thing = (Thing)objects[i];
                        var plant = thing.GetInnerIfMinified();
                        if (plant.MapHeld.listerBuildings.TryGetReinstallBlueprint(plant, out _))
                        {
                            continue;
                        }

                        return plant;
                    }
                }

                return null;
            }
        }

        protected new Thing ThingToInstall => MiniToInstallOrBuildingToReinstall.GetInnerIfMinified();

        public override BuildableDef PlacingDef => ThingToInstall.def;

        public override ThingStyleDef ThingStyleDefForPreview => ThingToInstall?.StyleDef;

        public override ThingDef StuffDef => null;

        public override bool Visible => MiniToInstallOrBuildingToReinstall != null;

        public override bool CanRemainSelected()
        {
            return MiniToInstallOrBuildingToReinstall != null;
        }

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
            var onArcanePlantPot = blockingThings.Any(v => v is ArcanePlantPot);

            foreach (var thing in blockingThings)
            {
                if (thing.def.category == ThingCategory.Building && !(thing is ArcanePlantPot) && thing.def.IsEdifice())
                {
                    return new AcceptanceReport("CannotBePlantedHere".Translate() + ": " + "BlockedBy".Translate(thing));
                }
            }

            var terrain = c.GetTerrain(Map);
            var isWaterPlant = terrain.IsWater && ThingToInstall.def.terrainAffordanceNeeded != null && terrain.affordances.Contains(ThingToInstall.def.terrainAffordanceNeeded);
            if (!onArcanePlantPot && !isWaterPlant && Map.fertilityGrid.FertilityAt(c) <= 0f)
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

                if (thing is Blueprint_Install blueprint_Install && blueprint_Install.ThingToInstall is ArcanePlant)
                {
                    return new AcceptanceReport("IdenticalThingExists".Translate());
                }
            }

            return GenConstruct.CanPlaceBlueprintAt(PlacingDef, c, placingRot, base.Map, godMode: false, MiniToInstallOrBuildingToReinstall, ThingToInstall);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var thingToInstall = MiniToInstallOrBuildingToReinstall;
            GenSpawn.WipeExistingThings(c, placingRot, thingToInstall.def.installBlueprintDef, base.Map, DestroyMode.Deconstruct);
            if (thingToInstall is MinifiedThing itemToInstall)
            {
                GenConstruct.PlaceBlueprintForInstall(itemToInstall, c, base.Map, placingRot, Faction.OfPlayer);
            }
            else
            {
                GenConstruct.PlaceBlueprintForReinstall((Building)thingToInstall, c, base.Map, placingRot, Faction.OfPlayer);
            }
            FleckMaker.ThrowMetaPuffs(GenAdj.OccupiedRect(c, placingRot, thingToInstall.def.Size), base.Map);

            if (Find.Selector.NumSelected == 1 || MiniToInstallOrBuildingToReinstall == null)
            {
                Find.DesignatorManager.Deselect();
            }
        }

        protected override void DrawGhost(Color ghostCol)
        {
            if (PlacingDef is ThingDef def)
            {
                MeditationUtility.DrawMeditationFociAffectedByBuildingOverlay(base.Map, def, Faction.OfPlayer, UI.MouseCell(), placingRot);
                GauranlenUtility.DrawConnectionsAffectedByBuildingOverlay(base.Map, def, Faction.OfPlayer, UI.MouseCell(), placingRot);
                PsychicRitualUtility.DrawPsychicRitualSpotsAffectedByThingOverlay(base.Map, def, UI.MouseCell(), placingRot);
            }

            Graphic baseGraphic = ThingToInstall.Graphic.ExtractInnerGraphicFor(ThingToInstall);
            GhostDrawer.DrawGhostThing(UI.MouseCell(), placingRot, (ThingDef)PlacingDef, baseGraphic, ghostCol, AltitudeLayer.Blueprint, ThingToInstall, drawPlaceWorkers: true, StuffDef);
        }

        protected override bool CanDrawNumbersBetween(Thing thing, ThingDef def, IntVec3 a, IntVec3 b, Map map)
        {
            if (ThingToInstall != thing)
            {
                return !GenThing.CloserThingBetween(def, a, b, map, ThingToInstall);
            }
            return false;
        }

        public override void SelectedUpdate()
        {
            base.SelectedUpdate();
            SectionLayer_ThingsManaFluxGrid.DrawManaFluxGridOverlayThisFrame();
        }
    }
}
