using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{

    public class GatherWorker_Plant : GatherWorker
    {
        public override string JobFailReasonIfNoHarvestable => LocalizeString_Etc.VV_JobFailReasonNoHarvestablePlants.Translate();

        public override Thing FilterGatherableTarget(Pawn pawn, Thing billGiver, Bill bill, IEnumerable<Thing> candidates)
        {
            foreach (var thing in candidates)
            {
                // Bill에 허용되지 않는 경우
                if (!bill.IsFixedOrAllowedIngredient(thing)) { continue; }

                // 식물 채집이 불가능한 경우
                if (!thing.CanGatherable(recipeDef.targetYieldStat, recipeDef.targetCooldownStat)) { continue; }

                // 식물이 병에 걸린 경우
                if (thing is Plant plant && plant.Blighted) { continue; }

                // 상호작용이 불가능한 경우
                if (thing.IsForbidden(pawn) || thing.IsBurning()) { continue; }

                // 접근 불가능한 경우
                if (!pawn.CanReserveAndReach(thing, PathEndMode.Touch, recipeDef.maxPathDanger)) { continue; }

                if (thing is IGatherableTarget gatherableTarget)
                {
                    if (!gatherableTarget.CanGatherByPawn(pawn, recipeDef)) { continue; }
                }

                return thing;
            }

            return null;
        }
    }
}
