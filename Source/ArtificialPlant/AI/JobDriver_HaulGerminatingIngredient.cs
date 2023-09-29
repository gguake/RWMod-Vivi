using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_HaulGerminatingIngredient : JobDriver
    {
        protected Thing IngredientThing => job.GetTarget(TargetIndex.A).Thing;
        protected Building_SeedlingGerminator Germinator => job.GetTarget(TargetIndex.B).Thing as Building_SeedlingGerminator;

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
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            AddFailCondition(() => Germinator.CurrentSchedule == null);

            yield return Toils_General.DoAtomic(() => { job.count = Germinator.GetGerminateRequiredCount(IngredientThing.def); });

            yield return Toils_Reserve.Reserve(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);

            yield return Toils_Haul.StartCarryThing(TargetIndex.A, subtractNumTakenFromJobCount: true);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(Toils_Reserve.Reserve(TargetIndex.A), TargetIndex.A, TargetIndex.None, takeFromValidStorage: true);

            yield return Toils_Reserve.Reserve(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);

            yield return Toils_General.Wait(240)
                .FailOnDestroyedNullOrForbidden(TargetIndex.B)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.B);

            yield return Toils_General.Do(() =>
            {
                Germinator.AddThings(IngredientThing);
            });
        }
    }
}
