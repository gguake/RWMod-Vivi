using RimWorld;
using Verse;

namespace VVRace
{
    public class GatherWorker_Pollen : GatherWorker_Plant
    {
        // 마법 식물(ArcanePlant: Building)은 꽃가루가 나오지 않으므로 일반 식물만 대상으로 한다.
        public override bool IsTargetDef(ThingDef def)
        {
            return def.thingClass != null && typeof(Plant).IsAssignableFrom(def.thingClass);
        }
    }
}
