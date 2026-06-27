using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{

    public class GatherWorker_Plant : GatherWorker
    {
        public override string JobFailReasonIfNoHarvestable => LocalizeString_Etc.VV_JobFailReasonNoHarvestablePlants.Translate();

        public override IEnumerable<Thing> FilterGatherableTarget(Thing billGiver, Bill bill, IEnumerable<Thing> candidates)
        {
            foreach (var thing in candidates)
            {
                // 식물 채집이 불가능한 경우
                if (!thing.CanGatherable(recipeDef.targetYieldStat, recipeDef.targetCooldownStat)) { continue; }

                // Bill에 허용되지 않는 경우
                if (!bill.IsFixedOrAllowedIngredient(thing)) { continue; }

                // 식물이 병에 걸린 경우
                if (thing is Plant plant && plant.Blighted) { continue; }

                yield return thing;
            }
        }
    }
}
