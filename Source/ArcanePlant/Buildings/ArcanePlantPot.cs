using RimWorld;
using System.Linq;
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
                        plant.Notify_ArcanePlantPotDespawned();
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
    }
}
