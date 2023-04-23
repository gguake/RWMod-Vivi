using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{

    public class GatherWorker_Plant : GatherWorker
    {
        public override string JobFailReasonIfNoHarvestable => LocalizeTexts.JobFailReasonNoHarvestablePlants.Translate();

        public override IEnumerable<Thing> FindAllGatherableTargetInRegion(Pawn pawn, Region region, Thing billGiver, Bill bill)
        {
            var allPlants = region.ListerThings.ThingsInGroup(ThingRequestGroup.Plant);
            var recipeGathering = bill.recipe as RecipeDef_Gathering;
            if (recipeGathering == null)
            {
                Log.Error($"RecipeDef is not RecipeDef_Gathering");
                yield break;
            }

            foreach (var thing in allPlants)
            {
                // 식물 채집이 불가능한 경우
                if (!thing.CanGatherable(recipeGathering.targetYieldStat, recipeGathering.targetCooldownStat)) { continue; }

                // 식물이 병에 걸린 경우
                if (thing is Plant plant && plant.Blighted) { continue; }

                // 상호작용이 불가능한 경우
                if (!thing.Spawned || thing.IsForbidden(pawn) || thing.IsBurning()) { continue; }

                // 거리가 너무 멀 경우
                if (bill.ingredientSearchRadius <= 100f && !pawn.Position.InHorDistOf(billGiver.Position, bill.ingredientSearchRadius)) { continue; }

                // 접근 불가능한 경우
                if (!ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, region, PathEndMode.Touch, pawn)) { continue; }

                // Bill에 허용되지 않는 경우
                if (!bill.IsFixedOrAllowedIngredient(thing) || !bill.recipe.ingredients.Any(v => v.filter.Allows(thing))) { continue; }

                // 예약이 불가능한 경우
                if (!pawn.CanReserve(thing)) { continue; }

                yield return thing;
            }
        }
    }
}
