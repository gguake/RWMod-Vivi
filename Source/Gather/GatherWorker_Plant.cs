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
        public override string JobFailReasonIfNoHarvestable => LocalizeTexts.JobFailReasonNoHarvestablePlants.Translate();

        public override IEnumerable<Thing> FindAllGatherableTargetInRegion(Region region)
        {   
            foreach (var thing in region.ListerThings.ThingsInGroup(ThingRequestGroup.NonStumpPlant).Concat(region.ListerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial)))
            {
                if (!thing.Spawned) { continue; }

                // 레시피가 허용되지 않는 경우
                if (!IsFixedOrAllowedIngredient(thing) || !recipeDef.ingredients.Any(v => v.filter.Allows(thing))) { continue; }

                // 식물 채집이 불가능한 경우
                if (!thing.CanGatherable(recipeDef.targetYieldStat, recipeDef.targetCooldownStat)) { continue; }

                // 식물이 병에 걸린 경우
                if (thing is Plant plant && plant.Blighted) { continue; }

                yield return thing;
            }
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

        [Obsolete]
        public override IEnumerable<Thing> FindAllGatherableTargetInRegion(Pawn pawn, Region region, Thing billGiver, Bill bill)
        {
            var allPlants = region.ListerThings.ThingsInGroup(ThingRequestGroup.Plant);

            foreach (var thing in allPlants)
            {
                // 거리가 너무 멀 경우
                if (!billGiver.InGatherableRange(thing)) { continue; }

                // Bill에 허용되지 않는 경우
                if (!bill.IsFixedOrAllowedIngredient(thing) || !bill.recipe.ingredients.Any(v => v.filter.Allows(thing))) { continue; }

                // 식물 채집이 불가능한 경우
                if (!thing.CanGatherable(recipeDef.targetYieldStat, recipeDef.targetCooldownStat)) { continue; }

                // 식물이 병에 걸린 경우
                if (thing is Plant plant && plant.Blighted) { continue; }

                // 상호작용이 불가능한 경우
                if (!thing.Spawned || thing.IsForbidden(pawn) || thing.IsBurning()) { continue; }

                // 접근 불가능한 경우
                if (!ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, region, PathEndMode.Touch, pawn)) { continue; }

                // 예약이 불가능한 경우
                if (!pawn.CanReserve(thing)) { continue; }

                yield return thing;
            }
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
