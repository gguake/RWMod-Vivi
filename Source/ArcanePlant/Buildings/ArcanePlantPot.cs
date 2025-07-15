using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace VVRace
{
    public class ArcanePlantPot : Building
    {
        protected ArcanePlantMapComponent _arcanePlantMapComp;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            _arcanePlantMapComp = map.GetComponent<ArcanePlantMapComponent>();
            _arcanePlantMapComp?.Notify_ArcanePlantPotSpawned(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            _arcanePlantMapComp?.Notify_ArcanePlantPotDespawned(this);
            _arcanePlantMapComp = null;

            if (mode != DestroyMode.WillReplace)
            {
                foreach (var cell in this.OccupiedRect().Cells)
                {
                    var thingList = cell.GetThingList(Map);
                    var plant = cell.GetFirstThing<ArcanePlant>(Map);
                    if (!plant.DestroyedOrNull())
                    {
                        if (plant.def.Minifiable)
                        {
                            plant.MinifyAndDropDirect();
                        }
                        else
                        {
                            plant.Destroy();
                        }
                    }

                    var blueprints = thingList.OfType<Blueprint>().ToList();
                    foreach (var blueprint in blueprints)
                    {
                        if (blueprint.def.entityDefToBuild is ThingDef thingDef && typeof(ArcanePlant).IsAssignableFrom(thingDef.thingClass))
                        {
                            blueprint.DeSpawn();
                        }
                    }
                }

            }

            base.DeSpawn(mode);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            var gizmos = base.GetGizmos();

            bool canMinify = true;
            if (Spawned)
            {
                foreach (var cell in this.OccupiedRect())
                {
                    if (cell.GetFirstThing<ArcanePlant_Everflower>(Map) != null)
                    {
                        canMinify = false;
                        break;
                    }
                }
            }

            if (Spawned)
            {
                foreach (var gizmo in gizmos)
                {
                    if ((gizmo is Designator_Install || gizmo is Designator_Uninstall) && !canMinify)
                    {
                        continue;
                    }

                    yield return gizmo;
                }
            }
            else
            {
                foreach (var gizmo in gizmos)
                {
                    yield return gizmo;
                }
            }

        }

        public override AcceptanceReport DeconstructibleBy(Faction faction)
        {
            if (Spawned)
            {
                foreach (var cell in this.OccupiedRect())
                {
                    if (cell.GetFirstThing<ArcanePlant_Everflower>(Map) != null)
                    {
                        return new AcceptanceReport(LocalizeString_Etc.VV_FailReasonToDeconstructEverflowerPotted.Translate());
                    }
                }

                return false;
            }

            return base.DeconstructibleBy(faction);
        }
    }
}
