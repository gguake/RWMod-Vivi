using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_FertilizeArcanePlant : JobDriver
    {
        public const int FertilizeTick = 400;

        protected ArcanePlant ArcanePlant => (ArcanePlant)FertilizeTarget;

        protected Thing FertilizeTarget => job.GetTarget(TargetIndex.A).Thing;
        protected Thing Fertilizer => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(FertilizeTarget, job, errorOnFailed: errorOnFailed))
            {
                return pawn.Reserve(Fertilizer, job, errorOnFailed: errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            AddEndCondition(() => (!ArcanePlant.IsFullMana) ? JobCondition.Ongoing : JobCondition.Succeeded);
            AddFailCondition(() => !job.playerForced && !ArcanePlant.ShouldAutoFertilizeNowIgnoringManaPct);
            AddFailCondition(() => !ArcanePlant.FertilizeAutoActivated);

            yield return Toils_General.DoAtomic(delegate
            {
                job.count = ArcanePlant.RequiredFertilizerToFullyRecharge;
            });

            Toil toilReserveFertilizer = Toils_Reserve.Reserve(TargetIndex.B);
            yield return toilReserveFertilizer;

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.B);

            yield return Toils_Haul.StartCarryThing(TargetIndex.B, putRemainderInQueue: false, subtractNumTakenFromJobCount: true)
                .FailOnDestroyedNullOrForbidden(TargetIndex.B);

            yield return Toils_Haul.CheckForGetOpportunityDuplicate(toilReserveFertilizer, TargetIndex.B, TargetIndex.None, takeFromValidStorage: true);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(FertilizeTick)
                .FailOnDestroyedNullOrForbidden(TargetIndex.B)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);

            var toilFinalizeFertilize = ToilMaker.MakeToil("FinalizeFertilize");
            toilFinalizeFertilize.defaultCompleteMode = ToilCompleteMode.Instant;
            toilFinalizeFertilize.initAction = () =>
            {
                var usedCount = Mathf.Min(ArcanePlant.RequiredFertilizerToFullyRecharge, Fertilizer.stackCount);

                ArcanePlant.AddMana(usedCount * ArcanePlant.ManaByFertilizer);
                Fertilizer.SplitOff(usedCount).Destroy();
            };

            yield return toilFinalizeFertilize;
        }
    }
}
