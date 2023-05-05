using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_GatheringBill : WorkGiver_DoBill
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override Danger MaxPathDanger(Pawn pawn)
            => Danger.Some;

        public override ThingRequest PotentialWorkThingRequest
            => def.fixedBillGiverDefs != null && def.fixedBillGiverDefs.Count == 1 ?
            ThingRequest.ForDef(def.fixedBillGiverDefs[0]) :
            ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!pawn.Spawned) { return true; }

            var buildings = def.fixedBillGiverDefs
                .Select(billGiverDef => pawn.Map.listerBuildings.AllBuildingsColonistOfDef(billGiverDef))
                .SelectMany(v => v)
                .ToList();

            foreach (var building in buildings)
            {
                if (building.IsForbidden(pawn) || !(building is IBillGiver billGiver)) { continue; }
                
                if (billGiver.BillStack.AnyShouldDoNow) { return false; }
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!(thing is IBillGiver billGiver)) { return null; }

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

                var targets = FindGatherableTargets(pawn, thing, bill);
                var target = recipeGathering.gatherWorker.FilterGatherableTarget(pawn, thing, bill, targets);

                if (target == null)
                {
                    JobFailReason.Is(recipeGathering.gatherWorker.JobFailReasonIfNoHarvestable);
                    continue;
                }

                if (recipeGathering.gatherWorker.TryMakeJob(pawn, thing, target, bill, out var job))
                {
                    return job;
                }
            }

            return null;
        }

        protected virtual IEnumerable<Thing> FindGatherableTargets(Pawn pawn, Thing billGiver, Bill bill)
        {
            var gatheringRecipe = bill.recipe as RecipeDef_Gathering;
            if (!(billGiver is Building_GatherWorkTable workTable))
            {
                return Enumerable.Empty<Thing>();
            }

            return workTable.GetGatherableCandidates(gatheringRecipe);
        }

        //protected virtual IEnumerable<Thing> FindGatherableTargets(Pawn pawn, Thing billGiver, Bill bill)
        //{
        //    var gatherWorker = (bill.recipe as RecipeDef_Gathering)?.gatherWorker;
        //    if (gatherWorker == null) { yield break; }

        //    HashSet<Thing> gatherTargets = new HashSet<Thing>();
        //    var billGiverRegion = (billGiver.def.hasInteractionCell ? billGiver.InteractionCell : billGiver.Position).GetRegion(billGiver.Map);

        //    var searchRadius = Building_GatherWorkTable.gatherRadius;
        //    RegionEntryPredicate regionEntryPredicate = (Region from, Region r) =>
        //    {
        //        if (!r.Allows(TraverseParms.For(pawn), isDestination: false)) { return false; }

        //        var extentsClose = r.extentsClose;
        //        int dx = Mathf.Abs(billGiver.Position.x - Mathf.Max(extentsClose.minX, Mathf.Min(billGiver.Position.x, extentsClose.maxX)));
        //        if (dx > searchRadius) { return false; }

        //        int dz = Mathf.Abs(billGiver.Position.z - Mathf.Max(extentsClose.minZ, Mathf.Min(billGiver.Position.z, extentsClose.maxZ)));
        //        if (dz > searchRadius) { return false; }

        //        return (dx * dx + dz * dz) <= searchRadius * searchRadius;
        //    };

        //    RegionTraverser.BreadthFirstTraverse(billGiverRegion, regionEntryPredicate, (r) =>
        //    {
        //        gatherTargets.AddRange(gatherWorker.FindAllGatherableTargetInRegion(pawn, r, billGiver, bill));

        //        if (gatherTargets.Any())
        //        {
        //            return true;
        //        }

        //        return false;

        //    }, maxRegions: 99999);

        //    foreach (var target in gatherTargets.OrderBy(thing => thing.Position.DistanceToSquared(pawn.Position)))
        //    {
        //        yield return target;
        //    }
        //}
    }
}
