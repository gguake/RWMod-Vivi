using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_CollectPerfumeScent : JobDriver
    {
        private const TargetIndex FlowerIndex = TargetIndex.A;
        private const TargetIndex BottleIndex = TargetIndex.B;

        private Thing Flower => job.GetTarget(FlowerIndex).Thing;
        private CompPerfumeBottle BottleComp => job.GetTarget(BottleIndex).Thing?.TryGetComp<CompPerfumeBottle>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Flower, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(FlowerIndex);
            this.FailOn(() => BottleComp == null || BottleComp.WearingPawn != pawn || !BottleComp.CanCollect);

            yield return Toils_Goto.GotoThing(FlowerIndex, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(FlowerIndex)
                .FailOnBurningImmobile(FlowerIndex);

            yield return Toils_General.Wait(BottleComp?.Props.gatherTicks ?? 1500, FlowerIndex)
                .FailOnDespawnedNullOrForbidden(FlowerIndex)
                .FailOnBurningImmobile(FlowerIndex)
                .WithFailCondition(() => BottleComp == null || !BottleComp.CanCollectFrom(pawn, Flower, out _))
                .WithEffect(VVEffecterDefOf.VV_Gather_Perfume, FlowerIndex)
                .WithProgressBarToilDelay(FlowerIndex);

            yield return Toils_General.DoAtomic(() => BottleComp?.TryCollect(Flower));
        }
    }
}
