using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_HaulAndManageSeedling : JobDriver
    {
        private float workLeft;

        protected Building_SeedlingGerminator Germinator => job.GetTarget(TargetIndex.A).Thing as Building_SeedlingGerminator;
        protected Thing IngredientThing => job.GetTarget(TargetIndex.B).Thing;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref workLeft, "workLeft");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(IngredientThing, job, errorOnFailed: errorOnFailed))
            {
                return pawn.Reserve(Germinator, job, errorOnFailed: errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Germinator.CurrentSchedule == null || !Germinator.CurrentSchedule.CanManageJob);

            yield return Toils_General.DoAtomic(() => { job.count = Germinator.CurrentSchedule.CurrentManageScheduleDef.ingredients[0].count; });

            var reserveToil = Toils_Reserve.Reserve(TargetIndex.B);
            yield return reserveToil;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.B);

            yield return Toils_Haul.StartCarryThing(TargetIndex.B, subtractNumTakenFromJobCount: true);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveToil, TargetIndex.B, TargetIndex.None, takeFromValidStorage: true);

            yield return Toils_Reserve.Reserve(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            var toilManage = ToilMaker.MakeToil("MakeNewToils");
            toilManage.initAction = () =>
            {
                workLeft = Germinator.CurrentSchedule.CurrentManageScheduleDef.workAmount;
            };
            toilManage.tickAction = () =>
            {
                var actor = GetActor();
                var curDriver = actor.jobs.curDriver;

                actor.skills.Learn(SkillDefOf.Plants, 0.01f);
                workLeft -= actor.GetStatValue(StatDefOf.PlantWorkSpeed);
                if (workLeft <= 0)
                {
                    curDriver.ReadyForNextToil();
                }
            };
            toilManage.defaultCompleteMode = ToilCompleteMode.Never;
            toilManage.WithProgressBar(TargetIndex.A, delegate
            {
                return 1f - workLeft / Germinator.CurrentSchedule.CurrentManageScheduleDef.workAmount;
            });

            yield return toilManage
                .FailOnDespawnedNullOrForbiddenPlacedThings(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);

            var toilFinalizeManage = ToilMaker.MakeToil("FinalizeManage");
            toilFinalizeManage.defaultCompleteMode = ToilCompleteMode.Instant;
            toilFinalizeManage.initAction = () =>
            {
                IngredientThing.SplitOff(Germinator.CurrentSchedule.CurrentManageScheduleDef.ingredients[0].count).Destroy();
                Germinator.CurrentSchedule.AdvanceGerminateSchedule(GetActor(), Germinator);
            };

            yield return toilFinalizeManage;
        }
    }
}
