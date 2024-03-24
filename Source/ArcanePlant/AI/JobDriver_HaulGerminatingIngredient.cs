using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_HaulGerminatingIngredient : JobDriver
    {
        protected Building_SeedlingGerminator Germinator => job.GetTarget(TargetIndex.A).Thing as Building_SeedlingGerminator;
        protected Thing IngredientThing => job.GetTarget(TargetIndex.B).Thing;

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
            AddFailCondition(() => Germinator.CurrentSchedule == null);

            yield return Toils_General.DoAtomic(() => { job.count = Germinator.GetGerminateRequiredCount(IngredientThing.GetInnerIfMinified().def); });

            var reserveToil = Toils_Reserve.Reserve(TargetIndex.B);
            yield return reserveToil;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.B);

            yield return Toils_Haul.StartCarryThing(TargetIndex.B, subtractNumTakenFromJobCount: true);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveToil, TargetIndex.B, TargetIndex.None, takeFromValidStorage: true);

            yield return Toils_Reserve.Reserve(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            yield return Toils_General.Wait(240)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.B, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);

            var toilFinalizeHauling = ToilMaker.MakeToil("FinalizeHaulingredient");
            toilFinalizeHauling.defaultCompleteMode = ToilCompleteMode.Instant;
            toilFinalizeHauling.initAction = () =>
            {
                Germinator.AddThings(IngredientThing);
            };

            yield return toilFinalizeHauling;
        }
    }
}
