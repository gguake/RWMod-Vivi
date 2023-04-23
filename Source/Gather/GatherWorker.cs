using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public abstract class GatherWorker
    {
        public abstract string JobFailReasonIfNoHarvestable { get; }

        public virtual bool PawnCanDoBill(Pawn pawn, Bill bill)
        {
            return pawn.GetStatValue(bill.recipe.workSpeedStat) > 0f && pawn.GetStatValue(bill.recipe.efficiencyStat) > 0f;
        }

        public abstract IEnumerable<Thing> FindAllGatherableTargetInRegion(Pawn pawn, Region region, Thing billGiver, Bill bill);

        public virtual bool TryMakeJob(Pawn pawn, Thing billGiver, IEnumerable<Thing> targets, Bill bill, out Job job)
        {
            var recipeGathering = bill.recipe as RecipeDef_Gathering;
            foreach (var target in targets)
            {
                if (!pawn.CanReserveAndReach(target, PathEndMode.Touch, recipeGathering.maxPathDanger)) { continue; }

                job = JobMaker.MakeJob(recipeGathering.gatheringJob, target, billGiver);
                job.bill = bill;
                job.haulMode = HaulMode.ToCellNonStorage;

                return true;
            }

            job = null;
            return false;
        }

        public virtual void Notify_Gathered(Pawn pawn, Thing billGiver, Thing target)
        {
            if (target is ThingWithComps thingWithComps)
            {
                var compGatherable = thingWithComps.GetComp<CompRepeatGatherable>();
                if (compGatherable != null)
                {
                    compGatherable.Gathered();
                }
            }
        }
    }
}
