using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ClearGerminator : JobDriver
    {
        public const int ClearTicks = 700;

        private const TargetIndex GerminatorIdx = TargetIndex.A;

        private Building_SeedlingGerminator Germinator => (Building_SeedlingGerminator)job.GetTarget(GerminatorIdx).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(GerminatorIdx);
            this.FailOnBurningImmobile(GerminatorIdx);
            this.FailOn(() => Germinator.CurrentSchedule == null || Germinator.ProductThingDef != null);

            yield return Toils_Goto.GotoThing(GerminatorIdx, PathEndMode.Touch);
            yield return Toils_General.Wait(ClearTicks)
                .FailOnDestroyedNullOrForbidden(GerminatorIdx)
                .FailOnCannotTouch(GerminatorIdx, PathEndMode.Touch)
                .WithProgressBarToilDelay(GerminatorIdx);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    Germinator.Clear();
                });
        }
    }
}
