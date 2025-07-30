using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class Filth_Pollen : Filth
    {
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            var map = Map;
            var reservers = new HashSet<Pawn>();
            Map.reservationManager.ReserversOf(this, reservers);

            var cleaner = reservers.Where(v => v.Position.DistanceToSquared(Position) < 4).FirstOrDefault();
            if (cleaner != null)
            {
                GatherPollen(map, cleaner);
            }

            base.DeSpawn(mode);
        }

        public void GatherPollen(Map map, Pawn gatherer)
        {
            var extension = def.GetModExtension<GatherableFilthExtension>();
            if (extension != null)
            {
                var stackCount = extension.amount * gatherer.GetStatValue(extension.yieldStat);
                var actualStackCount = (int)stackCount + (Rand.Chance(stackCount - (int)stackCount) ? 1 : 0);
                if (actualStackCount > 0)
                {
                    if (map.GetComponent<GatheringMapComponent>()[Position].gatherableWorkTables.Any(v => v.CanGatherFilth))
                    {
                        var thing = ThingMaker.MakeThing(extension.thingDef);
                        thing.stackCount = actualStackCount;

                        var placed = GenPlace.TryPlaceThing(thing, Position, map, ThingPlaceMode.Direct);
                        if (!placed)
                        {
                            thing.Destroy();
                        }
                    }
                }
            }
        }
    }
}
