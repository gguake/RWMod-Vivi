using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class HarvestWorker_Propolis : HarvestWorker
    {
        public override string JobFailReasonIfNoHarvestable => LocalizeTexts.JobFailReasonNoHarvestablePlants.Translate();

        public override bool CanDoBill(Pawn pawn, Bill bill)
            => pawn.HasViviGene();

        public override IEnumerable<Thing> FindAllHarvestTargetInRegion(Pawn pawn, Region region, Thing billGiver, Bill bill)
        {
            var allPlants = region.ListerThings.ThingsInGroup(ThingRequestGroup.Plant);

            foreach (var thing in allPlants)
            {
                // 나무가 아닌 경우
                if (thing.def.plant == null || !thing.def.plant.IsTree) { continue; }

                // 완전히 성장한 식물이 아닌 경우
                if (!(thing is Plant plant) || plant.Growth < 1f) { continue; }

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

            yield break;
        }

        public override bool TryMakeJob(Pawn pawn, Thing billGiver, IEnumerable<Thing> targets, Bill bill, out Job job)
        {
            var recipeHarvest = bill.recipe as RecipeDef_Harvest;
            foreach (var target in targets)
            {
                if (!pawn.CanReserveAndReach(target, PathEndMode.Touch, recipeHarvest.maxPathDanger)) { continue; }

                job = JobMaker.MakeJob(VVJobDefOf.VV_HarvestPropolis, target, billGiver);
                job.bill = bill;
                job.haulMode = HaulMode.ToCellNonStorage;

                return true;
            }

            job = null;
            return false;
        }
    }
}
