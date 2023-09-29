using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ManageSeedling : JobDriver
    {
        private float workLeft;

        private const TargetIndex GerminatorInd = TargetIndex.A;

        private Building_SeedlingGerminator Germinator => (Building_SeedlingGerminator)job.GetTarget(GerminatorInd).Thing;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref workLeft, "workLeft");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(GerminatorInd);
            this.FailOnBurningImmobile(GerminatorInd);
            this.FailOn(() => Germinator.CurrentSchedule == null || !Germinator.CurrentSchedule.CanManageJob);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            var toilManage = ToilMaker.MakeToil("MakeNewToils");
            toilManage.initAction = () =>
            {
                workLeft = Germinator.CurrentSchedule.CurrentManageScheduleDef.workAmount;
            };
            toilManage.tickAction = () =>
            {
                var actor = GetActor();
                var curDriver = actor.jobs.curDriver;

                actor.skills.Learn(SkillDefOf.Plants, 0.01f);
                workLeft -= actor.GetStatValue(StatDefOf.PlantWorkSpeed);
                if (workLeft <= 0)
                {
                    curDriver.ReadyForNextToil();
                }
            };
            toilManage.defaultCompleteMode = ToilCompleteMode.Never;
            toilManage.WithProgressBar(TargetIndex.A, delegate
            {
                return 1f - workLeft / Germinator.CurrentSchedule.CurrentManageScheduleDef.workAmount;
            });

            yield return toilManage
                .FailOnDespawnedNullOrForbiddenPlacedThings(GerminatorInd)
                .FailOnCannotTouch(GerminatorInd, PathEndMode.Touch);

            yield return Toils_General.Do(() =>
            {
                Germinator.CurrentSchedule.AdvanceGerminateSchedule(GetActor());
            });
        }
    }
}
