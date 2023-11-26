using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_EquipWeaponToShootus : JobDriver
    {
        public const int EquipTick = 400;

        protected Shootus Shootus => job.GetTarget(TargetIndex.A).Thing as Shootus;
        protected Thing Weapon => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(TargetB, job, errorOnFailed: errorOnFailed))
            {
                return pawn.Reserve(TargetA, job, errorOnFailed: errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            AddFailCondition(() => Shootus.Gun != null || Shootus.ReservedWeapon != Weapon);

            yield return Toils_Reserve.Reserve(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.B);

            yield return Toils_Haul.StartCarryThing(TargetIndex.B)
                .FailOnDestroyedNullOrForbidden(TargetIndex.B);

            yield return Toils_Reserve.Reserve(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            yield return Toils_General.Wait(EquipTick)
                .FailOnDestroyedNullOrForbidden(TargetIndex.B)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);

            var toilFinalizeEquip = ToilMaker.MakeToil("FinalizeEquip");
            toilFinalizeEquip.defaultCompleteMode = ToilCompleteMode.Instant;
            toilFinalizeEquip.initAction = () =>
            {
                Shootus.EquipWeapon(Weapon);
            };

            yield return toilFinalizeEquip;
        }
    }
}
