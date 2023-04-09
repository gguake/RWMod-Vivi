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
            return pawn.Reserve(Egg, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.B);

            yield return Toils_Goto.GotoThing(EggIndex, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(EggIndex);

            yield return Toils_Goto.GotoThing(HatcheryIndex, HatcheryPathEndMode);
            yield return Toils_Haul.PlaceHauledThingInCell(HatcheryIndex, null, storageMode: false);
            yield return Toils_General.Wait(100)
                .WithProgressBarToilDelay(TargetIndex.A)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnDestroyedNullOrForbidden(TargetIndex.B)
                .FailOnCannotTouch(TargetIndex.B, HatcheryPathEndMode);

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
