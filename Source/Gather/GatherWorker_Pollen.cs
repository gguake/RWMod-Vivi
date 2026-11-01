using RimWorld;
using Verse;

namespace VVRace
{
    public class GatherWorker_Pollen : GatherWorker_Plant
    {
        public override bool IsTargetDef(ThingDef def)
        {
            return def.thingClass != null && typeof(Plant).IsAssignableFrom(def.thingClass);
        }
    }
}
