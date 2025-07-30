using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_GatheringBill : WorkGiver_DoBill
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

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
                if (!bill.ShouldDoNow() || !bill.PawnAllowedToStartAnew(pawn) || !recipeGathering.gatherWorker.PawnCanDoBill(pawn, bill)) { continue; }

                var skillRequirement = bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn);
                if (skillRequirement != null)
                {
                    JobFailReason.Is("UnderRequiredSkill".Translate(skillRequirement.minLevel), bill.Label);
                    continue;
                }

                var candidates = thing.Map.GetComponent<GatheringMapComponent>().GetGatherableCandidatesForWorkTable((Building_GatherWorkTable)thing);
                var target = recipeGathering.gatherWorker.FilterGatherableTarget(
                    pawn, 
                    thing, 
                    bill, 
                    candidates);

                if (target == null)
                {
                    JobFailReason.Is(recipeGathering.gatherWorker.JobFailReasonIfNoHarvestable);
                    continue;
                }

                return recipeGathering.gatherWorker.MakeJob(pawn, thing, target, bill);
            }

            return null;
        }
    }
}
