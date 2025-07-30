using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Designator_ReplantArcanePlant : Designator_Install
    {
        public static Designator_ReplantArcanePlant GetCache(ThingDef thingDef)
        {
            if (!_cache.TryGetValue(thingDef, out var designator))
            {
                designator = new Designator_ReplantArcanePlant();
                designator.hotKey = KeyBindingDefOf.Misc1;
                _cache.Add(thingDef, designator);
            }

            return designator;
        }
        private static Dictionary<ThingDef, Designator_ReplantArcanePlant> _cache = new Dictionary<ThingDef, Designator_ReplantArcanePlant>();

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

                        return thing;
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

        public Designator_ReplantArcanePlant()
        {
            icon = TexCommand.Replant;
        }

        public override bool CanRemainSelected()
        {
            return MiniToInstallOrBuildingToReinstall != null;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            var canPlaceArcanePlantToCell = ArcanePlantUtility.CanPlaceArcanePlantToCell(Map, c, ThingToInstall.def);
            if (canPlaceArcanePlantToCell.Accepted == false)
            {
                return canPlaceArcanePlantToCell;
            }

            var thingToInstall = ThingToInstall;
            foreach (Thing thing in c.GetThingList(Map))
            {
                if (thing != thingToInstall)
                {
                    if (thing is ArcanePlantPot) { continue; }
                    if (thingToInstall.def.blocksAltitudes != null && thingToInstall.def.blocksAltitudes.Contains(thing.def.altitudeLayer))
                    {
                        return new AcceptanceReport("SpaceAlreadyOccupied".Translate());
                    }

                    var thingBuiltDef = GenConstruct.BuiltDefOf(thing.def);
                    if (thingBuiltDef?.blocksAltitudes != null && thingBuiltDef.blocksAltitudes.Contains(thingToInstall.def.altitudeLayer))
                    {
                        return new AcceptanceReport("SpaceAlreadyOccupied".Translate());
                    }

                    if (thing.def.EverHaulable || thingToInstall.def.ForceAllowPlaceOver(thing.def))
                    {
                        continue;
                    }

                    if (thing.def.category == ThingCategory.Plant && thing.def.passability == Traversability.Impassable)
                    {
                        return new AcceptanceReport("SpaceAlreadyOccupied".Translate());
                    }
                    if (thing.def.category == ThingCategory.Building || thing.def.IsBlueprint || thing.def.IsFrame)
                    {
                        if ((thing.def.building == null || thing.def.building.canBuildNonEdificesUnder) ||
                            (!thing.def.EverTransmitsPower || !thingToInstall.def.EverTransmitsPower))
                        {
                            continue;
                        }

                        return new AcceptanceReport("SpaceAlreadyOccupied".Translate());
                    }
                }
            }

            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var thingToInstall = MiniToInstallOrBuildingToReinstall;
            var placingDef = PlacingDef;

            GenSpawn.WipeExistingThings(c, placingRot, placingDef.installBlueprintDef, Map, DestroyMode.Deconstruct);
            if (thingToInstall is MinifiedThing minified)
            {
                GenConstruct.PlaceBlueprintForInstall(minified, c, Map, placingRot, Faction.OfPlayerSilentFail);
            }
            else
            {
                GenConstruct.PlaceBlueprintForReinstall((Building)thingToInstall, c, Map, placingRot, Faction.OfPlayerSilentFail);
            }
            FleckMaker.ThrowMetaPuffs(GenAdj.OccupiedRect(c, placingRot, placingDef.Size), Map);

            if (Find.Selector.NumSelected == 1 || MiniToInstallOrBuildingToReinstall == null)
            {
                Find.DesignatorManager.Deselect();
            }
        }

        protected override void DrawGhost(Color ghostCol)
        {
            if (PlacingDef is ThingDef def)
            {
                MeditationUtility.DrawMeditationFociAffectedByBuildingOverlay(base.Map, def, Faction.OfPlayerSilentFail, UI.MouseCell(), placingRot);
                GauranlenUtility.DrawConnectionsAffectedByBuildingOverlay(base.Map, def, Faction.OfPlayerSilentFail, UI.MouseCell(), placingRot);
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

            Map.GetComponent<ManaMapComponent>()?.MarkForDrawOverlay();
        }
    }
}
