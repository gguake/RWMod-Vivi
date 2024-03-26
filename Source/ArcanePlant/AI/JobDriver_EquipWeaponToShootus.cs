using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_EquipWeaponToShootus : JobDriver
    {
        public const int EquipTick = 400;

        private const TargetIndex ShootusIdx = TargetIndex.A;
        private const TargetIndex EquipmentIdx = TargetIndex.B;

        protected ArcanePlant_Shootus Shootus => job.GetTarget(ShootusIdx).Thing as ArcanePlant_Shootus;
        protected Thing Weapon => job.GetTarget(EquipmentIdx).Thing;

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
            this.FailOnDespawnedNullOrForbidden(ShootusIdx);
            AddFailCondition(() => Shootus.Gun != null || Shootus.ReservedWeapon != Weapon);

            yield return Toils_Reserve.Reserve(EquipmentIdx);
            yield return Toils_Goto.GotoThing(EquipmentIdx, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(EquipmentIdx)
                .FailOnSomeonePhysicallyInteracting(EquipmentIdx);

            yield return Toils_Haul.StartCarryThing(EquipmentIdx)
                .FailOnDestroyedNullOrForbidden(EquipmentIdx);

            yield return Toils_Reserve.Reserve(ShootusIdx);
            yield return Toils_Goto.GotoThing(ShootusIdx, PathEndMode.Touch);

            yield return Toils_General.Wait(EquipTick)
                .FailOnDestroyedNullOrForbidden(EquipmentIdx)
                .FailOnDestroyedNullOrForbidden(ShootusIdx)
                .FailOnCannotTouch(ShootusIdx, PathEndMode.Touch)
                .WithProgressBarToilDelay(ShootusIdx);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    Shootus.EquipGun(Weapon);
                });
        }
    }
}
