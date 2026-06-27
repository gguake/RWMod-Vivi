using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_GatheringBill : WorkGiver_DoBill
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        private List<Thing> _tmpGatheringBillTargetList = new List<Thing>();
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!(thing is IBillGiver billGiver) || !ThingIsUsableBillGiver(thing)) { return null; }

            if (!billGiver.BillStack.AnyShouldDoNow || !pawn.CanReserve(thing, ignoreOtherReservations: forced)) { return null; }
            if (thing.IsBurning() || thing.IsForbidden(pawn)) { return null; }
            if (thing.def.hasInteractionCell && !pawn.CanReserveSittableOrSpot(thing.InteractionCell, ignoreOtherReservations: forced)) { return null; }

            billGiver.BillStack.RemoveIncompletableBills();
            foreach (var bill in billGiver.BillStack)
            {
                if (!(bill.recipe is RecipeDef_Gathering recipeGathering) || recipeGathering.gatherWorker == null) { continue; }

                // early-exit
                if (Find.TickManager.TicksGame < bill.nextTickToSearchForIngredients && FloatMenuMakerMap.makingFor != pawn && !forced) { continue; }

                if (!bill.ShouldDoNow() || !bill.PawnAllowedToStartAnew(pawn) || !recipeGathering.gatherWorker.PawnCanDoBill(pawn, bill)) { continue; }

                var skillRequirement = bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn);
                if (skillRequirement != null)
                {
                    JobFailReason.Is("UnderRequiredSkill".Translate(skillRequirement.minLevel), bill.Label);
                    continue;
                }

                var candidates = thing.Map.GetComponent<GatheringMapComponent>().GetGatherableCandidatesForWorkTable((Building_GatherWorkTable)thing);
                if (candidates == null)
                {
                    continue;
                }

                recipeGathering.gatherWorker.FilterGatherableTarget(thing, bill, candidates).Where(v => !v.IsBurning()).CopyToList(_tmpGatheringBillTargetList);
                if (_tmpGatheringBillTargetList.Count == 0)
                {
                    bill.nextTickToSearchForIngredients = Find.TickManager.TicksGame + 600;

                    JobFailReason.Is(recipeGathering.gatherWorker.JobFailReasonIfNoHarvestable);
                    continue;
                }

                foreach (var target in _tmpGatheringBillTargetList)
                {
                    // 상호작용이 불가능한 경우
                    if (target.IsForbidden(pawn)) { continue; }

                    // 특수 조건
                    if (target is IGatherableTarget gatherableTarget && !gatherableTarget.CanGatherByPawn(pawn, recipeGathering)) { continue; }

                    // 접근 불가능한 경우
                    if (!pawn.CanReserveAndReach(target, PathEndMode.Touch, recipeGathering.maxPathDanger)) { continue; }

                    return recipeGathering.gatherWorker.MakeJob(pawn, thing, target, bill);
                }
            }

            return null;
        }
    }
}
