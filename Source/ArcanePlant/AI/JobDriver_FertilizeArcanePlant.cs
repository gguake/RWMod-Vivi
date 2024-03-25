using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_FertilizeArcanePlant : JobDriver
    {
        public const int FertilizeTick = 300;

        private const TargetIndex ArcanePlantIdx = TargetIndex.A;
        private const TargetIndex FertilizerIdx = TargetIndex.B;

        protected ArcanePlant ArcanePlant => job.GetTarget(ArcanePlantIdx).Thing as ArcanePlant;
        protected Thing Fertilizer => job.GetTarget(FertilizerIdx).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(TargetA, job, errorOnFailed: errorOnFailed))
            {
                return pawn.Reserve(TargetB, job, errorOnFailed: errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(ArcanePlantIdx);
            AddEndCondition(() => (!ArcanePlant.IsFullMana) ? JobCondition.Ongoing : JobCondition.Succeeded);
            AddFailCondition(() => !ArcanePlant.FertilizeAutoActivated || (!job.playerForced && !ArcanePlant.ShouldAutoFertilizeNowIgnoringManaPct));

            yield return Toils_General.DoAtomic(delegate
            {
                job.count = ArcanePlant.RequiredFertilizerToFullyRecharge;
            });

            Toil toilReserveFertilizer = Toils_Reserve.Reserve(FertilizerIdx);
            yield return toilReserveFertilizer;

            yield return Toils_Goto.GotoThing(FertilizerIdx, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(FertilizerIdx)
                .FailOnSomeonePhysicallyInteracting(FertilizerIdx);

            yield return Toils_Haul.StartCarryThing(FertilizerIdx, putRemainderInQueue: false, subtractNumTakenFromJobCount: true)
                .FailOnDestroyedNullOrForbidden(FertilizerIdx);

            yield return Toils_Haul.CheckForGetOpportunityDuplicate(toilReserveFertilizer, FertilizerIdx, TargetIndex.None, takeFromValidStorage: true);

            yield return Toils_Goto.GotoThing(ArcanePlantIdx, PathEndMode.Touch);
            yield return Toils_General.Wait(FertilizeTick)
                .FailOnDestroyedNullOrForbidden(FertilizerIdx)
                .FailOnDestroyedNullOrForbidden(ArcanePlantIdx)
                .FailOnCannotTouch(ArcanePlantIdx, PathEndMode.Touch)
                .WithProgressBarToilDelay(ArcanePlantIdx);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    var usedCount = Mathf.Min(ArcanePlant.RequiredFertilizerToFullyRecharge, Fertilizer.stackCount);

                    ArcanePlant.AddMana(usedCount * ArcanePlant.ManaByFertilizer);
                    Fertilizer.SplitOff(usedCount).Destroy();
                });
        }
    }
}
