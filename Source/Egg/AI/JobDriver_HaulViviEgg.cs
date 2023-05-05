using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_HaulViviEgg : JobDriver
    {
        private const TargetIndex EggIndex = TargetIndex.A;
        private const TargetIndex HatcheryIndex = TargetIndex.B;
        private const PathEndMode HatcheryPathEndMode = PathEndMode.OnCell;

        private LocalTargetInfo Egg => job.GetTarget(EggIndex);
        private LocalTargetInfo Hatchery => job.GetTarget(HatcheryIndex);


        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(Egg, job, errorOnFailed: errorOnFailed))
            {
                return pawn.Reserve(Hatchery, job, errorOnFailed: errorOnFailed);
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
            this.FailOn(() =>
            {
                var hatchery = Hatchery.Thing as ViviEggHatchery;
                return hatchery.ViviEgg != null;
            });

            yield return Toils_Reserve.Reserve(EggIndex);
            yield return Toils_Goto.GotoThing(EggIndex, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(EggIndex)
                .FailOnCannotTouch(EggIndex, PathEndMode.ClosestTouch);

            yield return Toils_Reserve.Reserve(HatcheryIndex);
            yield return Toils_Goto.GotoThing(HatcheryIndex, HatcheryPathEndMode);
            yield return Toils_Haul.PlaceHauledThingInCell(HatcheryIndex, null, storageMode: false);
            yield return Toils_General.Wait(100)
                .WithProgressBarToilDelay(TargetIndex.A)
                .FailOnCannotTouch(HatcheryIndex, PathEndMode.OnCell);

            var toil = new Toil();
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                var egg = Egg.Thing;
                var hatchery = Hatchery.Thing as ViviEggHatchery;
                hatchery.TryAcceptEgg(egg);
            };

            yield return toil;
        }
    }
}
