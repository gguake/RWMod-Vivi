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
                if (!thing.CanGatherable(recipeDef.targetYieldStat, recipeDef.targetCooldownStat)) { continue; }

                if (!bill.IsFixedOrAllowedIngredient(thing)) { continue; }

                if (thing is Plant plant && plant.Blighted) { continue; }

                yield return thing;
            }
        }
    }
}
