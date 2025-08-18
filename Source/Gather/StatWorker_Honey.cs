using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class StatWorker_GatherCooldown : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (!(req.Def is ThingDef thingDef)) { return 0f; }
            
            var value = base.GetValueUnfinalized(req, applyPostProcess);
            if (thingDef.plant != null)
            {
                var totalGrowDays = thingDef.plant.growDays;
                var totalLifeExpectancy = thingDef.plant.lifespanDaysPerGrowDays * totalGrowDays;
                var minGlow = thingDef.plant.growMinGlow;

                value *= Mathf.Max(0.01f, (totalLifeExpectancy - totalGrowDays) / (1f + minGlow) / 13.5f) * 4.9f;

                if (thingDef.plant.IsTree)
                {
                    value /= 2f;
                }
            }

            return value;
        }
    }

    public class StatWorker_HoneyYield : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (!(req.Def is ThingDef thingDef)) { return 0f; }

            var value = base.GetValueUnfinalized(req, applyPostProcess);
            if (thingDef.plant != null)
            {
                var minGlow = thingDef.plant.growMinGlow;
                var sowWork = thingDef.plant.sowWork;

                value *= Mathf.Max(0.1f, (1f + (sowWork / 225f - 1f) / 8f) + minGlow / 4f - 0.15f);

                if (thingDef.plant.harvestedThingDef != null)
                {
                    value /= 1.15f;
                }
            }

            return value;
        }
    }

    public class StatWorker_TreeResinGatherYield : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            if (!(req.Def is ThingDef thingDef)) { return 0f; }

            var value = base.GetValueUnfinalized(req, applyPostProcess);
            if (thingDef.plant != null)
            {
                var minGlow = thingDef.plant.growMinGlow;
                var sowWork = thingDef.plant.sowWork;
                var nutrition = thingDef.GetStatValueAbstract(StatDefOf.Nutrition);
                if (nutrition == 0f) { nutrition = 1f; }

                value *= Mathf.Max(0.01f, ((1f + (sowWork / 480f - 1f) / 5f) * (1f + minGlow) - 0.1f) * nutrition / 3f);
            }

            return value;
        }
    }
}
