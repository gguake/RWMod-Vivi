using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ClearGerminator : JobDriver
    {
        public const int ClearTicks = 500;

        private const TargetIndex GerminatorInd = TargetIndex.A;

        private Building_SeedlingGerminator Germinator => (Building_SeedlingGerminator)job.GetTarget(GerminatorInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(GerminatorInd);
            this.FailOnBurningImmobile(GerminatorInd);
            this.FailOn(() => Germinator.CurrentSchedule == null || Germinator.ProductThingDef != null);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(ClearTicks)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);

            var toilFinalizeClear = ToilMaker.MakeToil("FinalizeClear");
            toilFinalizeClear.defaultCompleteMode = ToilCompleteMode.Instant;
            toilFinalizeClear.initAction = () =>
            {
                Germinator.Clear();
            };
            yield return toilFinalizeClear;
        }
    }
}
