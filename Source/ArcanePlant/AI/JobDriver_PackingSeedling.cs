using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_PackingSeedling : JobDriver
    {
        public const int PackingTicks = 200;

        private const TargetIndex GerminatorInd = TargetIndex.A;

        private Building_SeedlingGerminator Germinator => (Building_SeedlingGerminator)job.GetTarget(GerminatorInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(GerminatorInd);
            this.FailOnBurningImmobile(GerminatorInd);
            this.FailOn(() => Germinator.CurrentSchedule == null || Germinator.ProductThingCount == 0 || !Germinator.CanWithdrawProduct);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(PackingTicks)
                .FailOnDestroyedNullOrForbidden(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A);

            var toilFinalizePacking = ToilMaker.MakeToil("FinalizePacking");
            toilFinalizePacking.defaultCompleteMode = ToilCompleteMode.Instant;
            toilFinalizePacking.initAction = () =>
            {
                var thing = Germinator.WithdrawProduct();

                GenSpawn.Spawn(thing, GetActor().Position, Map);
            };
            yield return toilFinalizePacking;
        }
    }
}
