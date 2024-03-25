using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ManageSeedling : JobDriver
    {
        private float workLeft;

        private const TargetIndex GerminatorIdx = TargetIndex.A;

        private Building_SeedlingGerminator Germinator => (Building_SeedlingGerminator)job.GetTarget(GerminatorIdx).Thing;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref workLeft, "workLeft");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(GerminatorIdx), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(GerminatorIdx);
            this.FailOnBurningImmobile(GerminatorIdx);
            this.FailOn(() => Germinator.CurrentSchedule == null || !Germinator.CurrentSchedule.CanManageJob);

            yield return Toils_Goto.GotoThing(GerminatorIdx, PathEndMode.Touch);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Never)
                .WithInitAction(() =>
                {
                    workLeft = Germinator.CurrentSchedule.CurrentManageScheduleDef.workAmount;
                })
                .WithTickAction(() =>
                {
                    var actor = GetActor();
                    var curDriver = actor.jobs.curDriver;

                    actor.skills.Learn(SkillDefOf.Plants, 0.01f);
                    workLeft -= actor.GetStatValue(StatDefOf.PlantWorkSpeed);
                    if (workLeft <= 0)
                    {
                        curDriver.ReadyForNextToil();
                    }
                })
                .WithProgressBar(GerminatorIdx, () =>
                {
                    return 1f - workLeft / Germinator.CurrentSchedule.CurrentManageScheduleDef.workAmount;
                })
                .FailOnDespawnedNullOrForbiddenPlacedThings(GerminatorIdx)
                .FailOnCannotTouch(GerminatorIdx, PathEndMode.Touch);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    Germinator.CurrentSchedule.AdvanceGerminateSchedule(GetActor(), Germinator);
                });
        }
    }
}
