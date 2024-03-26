using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_HaulViviEgg : JobDriver
    {
        private const TargetIndex EggIdx = TargetIndex.A;
        private const TargetIndex HatcheryIdx = TargetIndex.B;
        private const PathEndMode HatcheryPathEndMode = PathEndMode.OnCell;

        private LocalTargetInfo Egg => job.GetTarget(EggIdx);
        private LocalTargetInfo Hatchery => job.GetTarget(HatcheryIdx);


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
            this.FailOnDestroyedNullOrForbidden(EggIdx);
            this.FailOnDespawnedNullOrForbidden(HatcheryIdx);
            this.FailOn(() =>
            {
                var hatchery = Hatchery.Thing as ViviEggHatchery;
                return hatchery.ViviEgg != null;
            });

            yield return Toils_Reserve.Reserve(EggIdx);
            yield return Toils_Goto.GotoThing(EggIdx, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(EggIdx)
                .FailOnCannotTouch(EggIdx, PathEndMode.ClosestTouch);

            yield return Toils_Reserve.Reserve(HatcheryIdx);
            yield return Toils_Goto.GotoThing(HatcheryIdx, HatcheryPathEndMode);
            yield return Toils_Haul.PlaceHauledThingInCell(HatcheryIdx, null, storageMode: false);
            yield return Toils_General.Wait(100)
                .WithProgressBarToilDelay(EggIdx)
                .FailOnCannotTouch(HatcheryIdx, PathEndMode.OnCell);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    var egg = Egg.Thing;
                    var hatchery = Hatchery.Thing as ViviEggHatchery;
                    hatchery.TryAcceptEgg(egg);
                });
        }
    }
}
