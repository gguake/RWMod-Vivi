using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ReloadPerfumeBottle : JobDriver
    {
        private const TargetIndex BottleIndex = TargetIndex.A;
        private const TargetIndex PollenIndex = TargetIndex.B;

        private CompPerfumeBottle BottleComp => job.GetTarget(BottleIndex).Thing?.TryGetComp<CompPerfumeBottle>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(PollenIndex), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => BottleComp == null || BottleComp.WearingPawn != pawn);
            this.FailOnDestroyedOrNull(BottleIndex);
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);

            var getNextPollen = Toils_General.Label();
            yield return getNextPollen;
            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(PollenIndex);
            yield return Toils_Goto.GotoThing(PollenIndex, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(PollenIndex)
                .FailOnSomeonePhysicallyInteracting(PollenIndex);
            yield return Toils_General.Wait(BottleComp?.Props.reloadTicks ?? 300)
                .FailOnDespawnedNullOrForbidden(PollenIndex)
                .WithProgressBarToilDelay(BottleIndex);
            yield return Toils_General.DoAtomic(() => BottleComp.ReloadFrom(job.GetTarget(PollenIndex).Thing));
            yield return Toils_Jump.JumpIf(
                getNextPollen,
                () => BottleComp.NeedsRecharge && !job.GetTargetQueue(PollenIndex).NullOrEmpty());
        }
    }
}
