using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public interface IGatherableTarget
    {
        bool CanGatherByPawn(Pawn pawn, RecipeDef_Gathering recipe);

        void Notify_Gathered(Pawn pawn, RecipeDef_Gathering recipe);
    }

    public abstract class GatherWorker
    {
        public RecipeDef_Gathering recipeDef;

        public abstract string JobFailReasonIfNoHarvestable { get; }

        public virtual bool PawnCanDoBill(Pawn pawn, Bill bill)
        {
            return pawn.GetStatValue(bill.recipe.workSpeedStat) > 0f && pawn.GetStatValue(bill.recipe.efficiencyStat) > 0f;
        }

        public abstract IEnumerable<Thing> FindAllGatherableTargetInRegion(Region region);

        public abstract Thing FilterGatherableTarget(Pawn pawn, Thing billGiver, Bill bill, IEnumerable<Thing> candidates);

        public virtual Job MakeJob(Pawn pawn, Thing billGiver, Thing target, Bill bill)
        {
            var recipeGathering = bill.recipe as RecipeDef_Gathering;

            var job = JobMaker.MakeJob(recipeGathering.gatheringJob, target, billGiver);
            job.bill = bill;
            job.haulMode = HaulMode.ToCellNonStorage;

            return job;
        }

        public virtual void Notify_Gathered(Pawn pawn, Thing billGiver, Thing target, RecipeDef_Gathering recipe)
        {
            if (recipe.targetCooldownStat != null)
            {
                if (target is ThingWithComps thingWithComps)
                {
                    var compGatherable = thingWithComps.GetComp<CompRepeatGatherable>();
                    if (compGatherable != null)
                    {
                        compGatherable.Gathered(recipe.targetCooldownStat);
                    }
                }
            }


            if (target is IGatherableTarget notifyReceiver)
            {
                notifyReceiver.Notify_Gathered(pawn, recipe);
            }
        }

        public virtual void Notify_ProcessStarted(Pawn pawn)
        {
        }
    }
}
