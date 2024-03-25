using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_PackingSeedling : JobDriver
    {
        public const int PackingTicks = 200;

        private const TargetIndex GerminatorIdx = TargetIndex.A;

        private Building_SeedlingGerminator Germinator => (Building_SeedlingGerminator)job.GetTarget(GerminatorIdx).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(GerminatorIdx), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(GerminatorIdx);
            this.FailOnBurningImmobile(GerminatorIdx);
            this.FailOn(() => Germinator.CurrentSchedule == null || Germinator.ProductThingCount == 0 || !Germinator.CanWithdrawProduct);

            yield return Toils_Goto.GotoThing(GerminatorIdx, PathEndMode.Touch);
            yield return Toils_General.Wait(PackingTicks)
                .FailOnDestroyedNullOrForbidden(GerminatorIdx)
                .FailOnCannotTouch(GerminatorIdx, PathEndMode.Touch)
                .WithProgressBarToilDelay(GerminatorIdx);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    GenSpawn.Spawn(Germinator.WithdrawProduct(), GetActor().Position, Map);
                });
        }
    }
}
