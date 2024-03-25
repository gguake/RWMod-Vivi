using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_HaulGerminatingIngredient : JobDriver
    {
        private const TargetIndex GerminatorIdx = TargetIndex.A;
        private const TargetIndex IngredientIdx = TargetIndex.B;

        protected Building_SeedlingGerminator Germinator => job.GetTarget(GerminatorIdx).Thing as Building_SeedlingGerminator;
        protected Thing IngredientThing => job.GetTarget(IngredientIdx).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(TargetB, job, errorOnFailed: errorOnFailed))
            {
                return pawn.Reserve(TargetA, job, errorOnFailed: errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(GerminatorIdx);
            AddFailCondition(() => Germinator.CurrentSchedule == null);

            yield return Toils_General.DoAtomic(() => { job.count = Germinator.GetGerminateRequiredCount(IngredientThing.GetInnerIfMinified().def); });

            var reserveToil = Toils_Reserve.Reserve(IngredientIdx);
            yield return reserveToil;
            yield return Toils_Goto.GotoThing(IngredientIdx, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(IngredientIdx)
                .FailOnSomeonePhysicallyInteracting(IngredientIdx);

            yield return Toils_Haul.StartCarryThing(IngredientIdx, subtractNumTakenFromJobCount: true);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveToil, IngredientIdx, TargetIndex.None, takeFromValidStorage: true);

            yield return Toils_Reserve.Reserve(GerminatorIdx);
            yield return Toils_Goto.GotoThing(GerminatorIdx, PathEndMode.Touch);

            yield return Toils_General.Wait(240)
                .FailOnDestroyedNullOrForbidden(GerminatorIdx)
                .FailOnCannotTouch(IngredientIdx, PathEndMode.Touch)
                .WithProgressBarToilDelay(GerminatorIdx);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    Germinator.AddThings(IngredientThing);
                });
        }
    }
}
