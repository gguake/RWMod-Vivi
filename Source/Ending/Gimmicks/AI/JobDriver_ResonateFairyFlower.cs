using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ResonateFairyFlower : JobDriver
    {
        public const int ResonateTicks = 180;

        private const TargetIndex FlowerIndex = TargetIndex.A;

        private Building_FairyFlower Flower => job.GetTarget(FlowerIndex).Thing as Building_FairyFlower;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(FlowerIndex), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(FlowerIndex);
            this.FailOn(() => Flower == null || !Flower.CanResonate(pawn).Accepted);

            yield return Toils_Goto.GotoThing(FlowerIndex, PathEndMode.Touch);

            yield return Toils_General.Wait(ResonateTicks, FlowerIndex)
                .WithProgressBarToilDelay(FlowerIndex)
                .WithEffect(VVEffecterDefOf.VV_Effecter_FairyFlowerResonance, FlowerIndex);

            yield return Toils_General.DoAtomic(() => Flower?.Notify_Interacted(pawn));
        }
    }
}
