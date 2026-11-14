using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_CollectFloralScent : JobDriver
    {
        private const TargetIndex FlowerIndex = TargetIndex.A;
        private const TargetIndex PollenIndex = TargetIndex.B;

        private Thing Flower => job.GetTarget(FlowerIndex).Thing;

        private CompScentBottle _bottle;
        private CompScentBottle Bottle => _bottle ?? (_bottle = ScanForBottle());

        private CompScentBottle ScanForBottle()
        {
            var wornApparel = pawn.apparel?.WornApparel;
            if (wornApparel == null) { return null; }

            for (int i = 0; i < wornApparel.Count; ++i)
            {
                var comp = wornApparel[i].TryGetComp<CompScentBottle>();
                if (comp != null) { return comp; }
            }

            return null;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Flower, job, errorOnFailed: errorOnFailed)) { return false; }

            // 스택 전체가 아니라 가져갈 양만 예약해 남은 꽃가루를 다른 폰이 쓸 수 있게 한다.
            var pollenQueue = job.GetTargetQueue(PollenIndex);
            for (int i = 0; i < pollenQueue.Count; ++i)
            {
                pawn.Reserve(
                    pollenQueue[i], job, CompScentBottle.PollenReserveMaxPawns, job.countQueue[i], null, errorOnFailed: false);
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(FlowerIndex);
            this.FailOn(() => Bottle == null || Bottle.WearingPawn != pawn);

            var gotoFlower = Toils_General.Label();
            var extractPollen = Toils_JobTransforms.ExtractNextTargetFromQueue(PollenIndex, failIfCountFromQueueTooBig: false);

            yield return Toils_Jump.JumpIf(gotoFlower, () => Bottle.PollenCharged);
            yield return extractPollen;
            yield return Toils_Goto.GotoThing(PollenIndex, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(PollenIndex)
                .FailOnSomeonePhysicallyInteracting(PollenIndex);
            yield return Toils_General.Wait(Bottle?.Props.loadTicks ?? 300)
                .FailOnDespawnedNullOrForbidden(PollenIndex)
                .WithProgressBarToilDelay(PollenIndex);
            yield return Toils_General.DoAtomic(() =>
            {
                var pollen = job.GetTarget(PollenIndex).Thing;
                Bottle.LoadPollenFrom(pollen, job.count);

                // 다 실은 뒤에도 예약이 남으면 향 채집이 끝날 때까지 남은 꽃가루가 묶인다.
                var reservations = pawn.Map.reservationManager;
                if (pollen != null && !pollen.Destroyed && reservations.ReservedBy(pollen, pawn, job))
                {
                    reservations.Release(pollen, pawn, job);
                }
            });
            yield return Toils_Jump.JumpIf(
                extractPollen,
                () => !Bottle.PollenCharged && !job.GetTargetQueue(PollenIndex).NullOrEmpty());
            yield return Toils_General.DoAtomic(() =>
            {
                if (!Bottle.PollenCharged)
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            });

            yield return gotoFlower;
            yield return Toils_Goto.GotoThing(FlowerIndex, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(FlowerIndex)
                .FailOnBurningImmobile(FlowerIndex);
            yield return Toils_General.Wait(Bottle?.Props.gatherTicks ?? 1500, FlowerIndex)
                .FailOnDespawnedNullOrForbidden(FlowerIndex)
                .FailOnBurningImmobile(FlowerIndex)
                .WithFailCondition(() => Bottle == null || !Bottle.PollenCharged)
                .WithEffect(VVEffecterDefOf.VV_Gather_Scent, FlowerIndex)
                .WithProgressBarToilDelay(FlowerIndex);
            yield return Toils_General.DoAtomic(() => Bottle?.CompleteCollect(Flower));
        }
    }
}
