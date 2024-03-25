using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_HaulAndManageSeedling : JobDriver
    {
        private float workLeft;

        private const TargetIndex GerminatorIdx = TargetIndex.A;
        private const TargetIndex IngredientIdx = TargetIndex.B;

        protected Building_SeedlingGerminator Germinator => job.GetTarget(GerminatorIdx).Thing as Building_SeedlingGerminator;
        protected Thing Ingredient => job.GetTarget(TargetIndex.B).Thing;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref workLeft, "workLeft");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(Ingredient, job, errorOnFailed: errorOnFailed))
            {
                return pawn.Reserve(Germinator, job, errorOnFailed: errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(GerminatorIdx);
            this.FailOnBurningImmobile(GerminatorIdx);
            this.FailOn(() => Germinator.CurrentSchedule == null || !Germinator.CurrentSchedule.CanManageJob);

            yield return Toils_General.DoAtomic(() => { job.count = Germinator.CurrentSchedule.CurrentManageScheduleDef.ingredients[0].count; });

            var reserveToil = Toils_Reserve.Reserve(IngredientIdx);
            yield return reserveToil;
            yield return Toils_Goto.GotoThing(IngredientIdx, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(IngredientIdx)
                .FailOnSomeonePhysicallyInteracting(IngredientIdx);

            yield return Toils_Haul.StartCarryThing(IngredientIdx, subtractNumTakenFromJobCount: true);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveToil, IngredientIdx, TargetIndex.None, takeFromValidStorage: true);

            yield return Toils_Reserve.Reserve(GerminatorIdx);
            yield return Toils_Goto.GotoThing(GerminatorIdx, PathEndMode.Touch);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Never)
                .WithInitAction(() =>
                {
                    workLeft = Germinator.CurrentSchedule.CurrentManageScheduleDef.workAmount;
                })
                .WithTickAction(() =>
                {
                    var actor = GetActor();
                    var curDriver = actor.jobs.curDriver;

                    actor.skills.Learn(SkillDefOf.Plants, 0.01f);
                    workLeft -= actor.GetStatValue(StatDefOf.PlantWorkSpeed);
                    if (workLeft <= 0)
                    {
                        curDriver.ReadyForNextToil();
                    }
                })
                .WithProgressBar(GerminatorIdx, () =>
                {
                    return 1f - workLeft / Germinator.CurrentSchedule.CurrentManageScheduleDef.workAmount;
                })
                .FailOnDespawnedNullOrForbiddenPlacedThings(GerminatorIdx)
                .FailOnCannotTouch(GerminatorIdx, PathEndMode.Touch);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    Ingredient.SplitOff(Germinator.CurrentSchedule.CurrentManageScheduleDef.ingredients[0].count).Destroy();
                    Germinator.CurrentSchedule.AdvanceGerminateSchedule(GetActor(), Germinator);
                });
        }
    }
}
