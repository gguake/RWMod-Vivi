using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ArtificialPlantPot : Building
    {
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            foreach (var cell in this.OccupiedRect().Cells)
            {
                var thingList = cell.GetThingList(Map);

                var plants = thingList.OfType<ArtificialPlant>().ToList();
                foreach (var plant in plants)
                {
                    var minified = plant.MakeMinified();
                    if (!GenPlace.TryPlaceThing(minified, Position, Map, ThingPlaceMode.Near))
                    {
                        Log.Warning($"try place minified artifical plant {plant} but failed");
                    }
                }

                var blueprints = thingList.OfType<Blueprint>().ToList();
                foreach (var blueprint in blueprints)
                {
                    if (blueprint.def.entityDefToBuild is ThingDef thingDef && typeof(ArtificialPlant).IsAssignableFrom(thingDef.thingClass))
                    {
                        blueprint.DeSpawn();
                    }
                }
            }

            base.DeSpawn(mode);
        }
    }
}
