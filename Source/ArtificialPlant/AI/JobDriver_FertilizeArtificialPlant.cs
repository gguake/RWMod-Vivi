using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_FertilizeArtificialPlant : JobDriver
    {
        public const int FertilizeTick = 400;

        protected ArtificialPlant ArtificialPlant => (ArtificialPlant)FertilizeTarget;

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
            AddEndCondition(() => (!ArtificialPlant.IsFullEnergy) ? JobCondition.Ongoing : JobCondition.Succeeded);
            AddFailCondition(() => !job.playerForced && !ArtificialPlant.ShouldAutoFertilizeNowIgnoringEnergyPct);
            AddFailCondition(() => !ArtificialPlant.FertilizeAutoActivated && !job.playerForced);

            yield return Toils_General.DoAtomic(delegate
            {
                job.count = ArtificialPlant.RequiredFertilizerToFullyRecharge;
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
                var usedCount = Mathf.Min(ArtificialPlant.RequiredFertilizerToFullyRecharge, Fertilizer.stackCount);

                ArtificialPlant.AddEnergy(usedCount * ArtificialPlant.EnergyByFertilizer);
                Fertilizer.SplitOff(usedCount).Destroy();
            };

            yield return toilFinalizeFertilize;
        }
    }
}
