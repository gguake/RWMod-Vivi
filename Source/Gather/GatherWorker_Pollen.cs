using RimWorld;
using Verse;

namespace VVRace
{
    public class GatherWorker_Pollen : GatherWorker_Plant
    {
        // ArcanePlant는 Building이며 꽃가루를 생산하지 않으므로 일반 식물만 대상으로 삼는다.
        public override bool IsTargetDef(ThingDef def)
        {
            return def.thingClass != null && typeof(Plant).IsAssignableFrom(def.thingClass);
        }
    }
}
