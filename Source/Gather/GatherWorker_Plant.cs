using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{

    public class GatherWorker_Plant : GatherWorker
    {
        public override string JobFailReasonIfNoHarvestable => LocalizeString_Etc.VV_JobFailReasonNoHarvestablePlants.Translate();

        public override IEnumerable<Thing> FindAllGatherableTargetInRegion(Region region)
        {
            var gatherables = region.ListerThings.ThingsInGroup(ThingRequestGroup.NonStumpPlant);
            var arcanePlants = region.ListerThings.ThingsInGroup(ThingRequestGroup.WithCustomRectForSelector);

            for (int i = 0; i < gatherables.Count; ++i)
            {
                if (IsGatherableTargetInRegion(region, gatherables[i]))
                {
                    yield return gatherables[i];
                }
            }

            for (int i = 0; i < arcanePlants.Count; ++i)
            {
                if (arcanePlants[i] is ArcanePlant && IsGatherableTargetInRegion(region, arcanePlants[i]))
                {
                    yield return arcanePlants[i];
                }
            }
        }

        private bool IsGatherableTargetInRegion(Region region, Thing thing)
        {
            if (!thing.Spawned) { return false; }

            // 레시피가 허용되지 않는 경우
            if (!IsFixedOrAllowedIngredient(thing) || !recipeDef.ingredients.Any(v => v.filter.Allows(thing))) { return false; }

            // 식물 채집이 불가능한 경우
            if (!thing.CanGatherable(recipeDef.targetYieldStat, recipeDef.targetCooldownStat)) { return false; }

            // 식물이 병에 걸린 경우
            if (thing is Plant plant && plant.Blighted) { return false; }

            return true;
        }

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

                return thing;
            }

            return null;
        }

        private bool IsFixedOrAllowedIngredient(Thing thing)
        {
            foreach (var ingredientCount in recipeDef.ingredients)
            {
                if (ingredientCount.IsFixedIngredient && ingredientCount.filter.Allows(thing))
                {
                    return true;
                }
            }

            if (recipeDef.fixedIngredientFilter.Allows(thing))
            {
                return true;
            }

            return false;
        }
    }
}
