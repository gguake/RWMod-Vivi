using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_DeliverPawnToEverflower : JobDriver
    {
        private const TargetIndex TargetPawnIndex = TargetIndex.A;
        private const TargetIndex DestCellIndex = TargetIndex.B;
        private const TargetIndex EverflowerIndex = TargetIndex.C;

        private Pawn TargetPawn => (Pawn)job.GetTarget(TargetPawnIndex).Thing;
        private ArcanePlant_Everflower Everflower => (ArcanePlant_Everflower)job.GetTarget(EverflowerIndex).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            base.Map.reservationManager.ReleaseAllForTarget(TargetPawn);
            if (pawn.Reserve(TargetPawn, job, 1, -1, null, errorOnFailed))
            {
                return pawn.Reserve(Everflower, job, 1, 0, null, errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetPawnIndex);
            this.FailOnDestroyedOrNull(EverflowerIndex);
            this.FailOnAggroMentalStateAndHostile(TargetPawnIndex);

            yield return Toils_Goto.GotoThing(TargetPawnIndex, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetPawnIndex)
                .FailOnDespawnedNullOrForbidden(EverflowerIndex)
                .FailOn(() => !pawn.CanReach(Everflower, PathEndMode.OnCell, Danger.Deadly))
                .FailOnSomeonePhysicallyInteracting(TargetPawnIndex);

            var goToAltar = Toils_Goto.GotoThing(EverflowerIndex, PathEndMode.ClosestTouch);
            yield return Toils_Jump.JumpIf(goToAltar, () => pawn.IsCarryingPawn(TargetPawn));
            yield return Toils_Haul.StartCarryThing(TargetPawnIndex);
            yield return goToAltar;

            yield return Toils_General.Do(delegate
            {
                job.SetTarget(DestCellIndex, Everflower.Position);
            });

            yield return Toils_Reserve.Release(EverflowerIndex);
            yield return Toils_Haul.PlaceHauledThingInCell(DestCellIndex, null, storageMode: false);
        }
    }
}
