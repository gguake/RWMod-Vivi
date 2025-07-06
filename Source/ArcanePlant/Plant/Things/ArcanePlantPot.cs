using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ArcanePlantPot : Building
    {
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            foreach (var cell in this.OccupiedRect().Cells)
            {
                var thingList = cell.GetThingList(Map);

                var plants = thingList.OfType<ArcanePlant>().ToList();
                foreach (var plant in plants)
                {
                    plant.MinifyAndDropDirect();
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

            base.DeSpawn(mode);
        }
    }
}
