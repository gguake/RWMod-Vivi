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

        private const TargetIndex GerminatorInd = TargetIndex.A;
        private const TargetIndex IngredientsInd = TargetIndex.B;
        private const TargetIndex IngredientsPlaceCellInd = TargetIndex.C;

        private Building_SeedlingGerminator Germinator => (Building_SeedlingGerminator)job.GetTarget(GerminatorInd).Thing;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref workLeft, "workLeft");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.GetTarget(GerminatorInd), job))
            {
                return false;
            }

            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(IngredientsInd), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(GerminatorInd);
            this.FailOnBurningImmobile(GerminatorInd);
            this.FailOn(() => Germinator.CurrentSchedule == null || !Germinator.CurrentSchedule.CanManageJob);

            var gotoGerminatorToil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Jump.JumpIf(gotoGerminatorToil, () => job.GetTargetQueue(IngredientsInd).NullOrEmpty());

            foreach (var toil in CollectIngredientsToils())
            {
                yield return toil;
            }

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
                .FailOnDespawnedNullOrForbiddenPlacedThings(GerminatorInd)
                .FailOnCannotTouch(GerminatorInd, PathEndMode.Touch);

            yield return Toils_General.Do(() =>
            {
                var list = new List<Thing>();
                if (job.placedThings != null)
                {
                    for (int i = 0; i < job.placedThings.Count; ++i)
                    {
                        var thing = (job.placedThings[i].Count >= job.placedThings[i].thing.stackCount) ? 
                            job.placedThings[i].thing : 
                            job.placedThings[i].thing.SplitOff(job.placedThings[i].Count);

                        job.placedThings[i].Count = 0;

                        list.Add(thing);
                    }
                }
                job.placedThings = null;

                foreach (var thing in list)
                {
                    thing.Destroy();
                }

                Germinator.CurrentSchedule.AdvanceGerminateSchedule(GetActor());
            });
        }

        private IEnumerable<Toil> CollectIngredientsToils()
        {
            var extractToil = Toils_JobTransforms.ExtractNextTargetFromQueue(IngredientsInd);
            var jumpIfHaveTargetInQueueToil = Toils_Jump.JumpIfHaveTargetInQueue(IngredientsInd, extractToil);

            yield return extractToil;

            var gotoToil = Toils_Goto.GotoThing(IngredientsInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(IngredientsInd)
                .FailOnSomeonePhysicallyInteracting(IngredientsInd);

            yield return gotoToil;

            yield return Toils_Haul.StartCarryThing(IngredientsInd, putRemainderInQueue: true, failIfStackCountLessThanJobCount: true);
            yield return JobDriver_DoBill.JumpToCollectNextIntoHandsForBill(gotoToil, IngredientsInd);

            yield return Toils_Goto.GotoThing(GerminatorInd, PathEndMode.Touch)
                .FailOnDestroyedNullOrForbidden(IngredientsInd);

            var findPlaceTargetToil = Toils_JobTransforms.SetTargetToIngredientPlaceCell(GerminatorInd, IngredientsInd, IngredientsPlaceCellInd);
            yield return findPlaceTargetToil;

            yield return Toils_Haul.PlaceHauledThingInCell(IngredientsPlaceCellInd, findPlaceTargetToil, storageMode: false);

            var reserveToil = ToilMaker.MakeToil("CollectIngredientsToils");
            reserveToil.initAction = () =>
            {
                GetActor().Map.physicalInteractionReservationManager.Reserve(GetActor(), job, job.GetTarget(IngredientsInd));
            };
            yield return reserveToil;

            yield return jumpIfHaveTargetInQueueToil;
        }
    }
}
