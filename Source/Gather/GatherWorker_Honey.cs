using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using VVRace.Honey;

namespace VVRace
{
    public class GatherWorker_Honey : GatherWorker
    {
        public override string JobFailReasonIfNoHarvestable => LocalizeTexts.JobFailReasonNoHarvestablePlants.Translate();

        public override bool CanDoBill(Pawn pawn, Bill bill)
            => pawn.HasViviGene();

        public override IEnumerable<Thing> FindAllGatherableTargetInRegion(Pawn pawn, Region region, Thing billGiver, Bill bill)
        {
            var allPlants = region.ListerThings.ThingsInGroup(ThingRequestGroup.Plant);

            foreach (var thing in allPlants)
            {
                // 꿀 채집이 불가능한 경우
                if (!thing.CanGatherable(VVStatDefOf.VV_PlantHoneyGatherYield, VVStatDefOf.VV_PlantGatherCooldown)) { continue; }

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

        public override bool TryMakeJob(Pawn pawn, Thing billGiver, IEnumerable<Thing> targets, Bill bill, out Job job)
        {
            var recipeGathering = bill.recipe as RecipeDef_Gathering;
            foreach (var target in targets)
            {
                if (!pawn.CanReserveAndReach(target, PathEndMode.Touch, recipeGathering.maxPathDanger)) { continue; }

                job = JobMaker.MakeJob(VVJobDefOf.VV_GatherHoney, target, billGiver);
                job.bill = bill;
                job.haulMode = HaulMode.ToCellNonStorage;

                return true;
            }

            job = null;
            return false;
        }

        public override bool ShouldAddRecipeIngredient(ThingDef thingDef)
        {
            return thingDef.StatBaseDefined(VVStatDefOf.VV_PlantHoneyGatherYield) &&
                thingDef.GetStatValueAbstract(VVStatDefOf.VV_PlantHoneyGatherYield) > 0f;
        }
    }
}
