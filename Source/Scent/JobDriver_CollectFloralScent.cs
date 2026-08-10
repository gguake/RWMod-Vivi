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

            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(PollenIndex), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(FlowerIndex);
            this.FailOn(() => Bottle == null || Bottle.WearingPawn != pawn);

            var gotoFlower = Toils_General.Label();
            var extractPollen = Toils_JobTransforms.ExtractNextTargetFromQueue(PollenIndex);

            yield return Toils_Jump.JumpIf(gotoFlower, () => Bottle.PollenCharged);
            yield return extractPollen;
            yield return Toils_Goto.GotoThing(PollenIndex, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(PollenIndex)
                .FailOnSomeonePhysicallyInteracting(PollenIndex);
            yield return Toils_General.Wait(Bottle?.Props.loadTicks ?? 300)
                .FailOnDespawnedNullOrForbidden(PollenIndex)
                .WithProgressBarToilDelay(PollenIndex);
            yield return Toils_General.DoAtomic(() => Bottle.LoadPollenFrom(job.GetTarget(PollenIndex).Thing));
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
