using Verse;

namespace VVRace
{
    public class PlaceWorker_EnergyTube : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            var thingList = loc.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                if (thingList[i] is EnergyAcceptor)
                {
                    return false;
                }

                if (thingList[i].def.entityDefToBuild != null && thingList[i].def.entityDefToBuild is ThingDef thingDef && typeof(EnergyAcceptor).IsAssignableFrom(thingDef.thingClass))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
