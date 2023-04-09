using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public abstract class JobDriver_InteractTarget : JobDriver
    {
        protected abstract bool CanProgressJob { get; }
        protected abstract float ProgressByTick { get; }
        protected abstract float WorkTotal { get; }

        protected virtual TargetIndex MainTargetIndex => TargetIndex.A;
        protected LocalTargetInfo MainTargetInfo => job.GetTarget(MainTargetIndex);
        protected Thing MainTargetThing => MainTargetInfo.Thing;

        private float progress;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref progress, "progress", 0f);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(MainTargetInfo, job, errorOnFailed: errorOnFailed);
        }

        protected abstract void OnJobCompleted();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(MainTargetIndex);
            this.FailOnDowned(MainTargetIndex);
            this.FailOnNotCasualInterruptible(MainTargetIndex);

            yield return Toils_Goto.GotoThing(MainTargetIndex, PathEndMode.Touch);

            var toil = ToilMaker.MakeToil();
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.initAction = delegate
            {
                var target = MainTargetThing;
                if (target != null)
                {
                    pawn.pather?.StopDead();

                    if (target is Pawn targetPawn)
                    {
                        PawnUtility.ForceWait(targetPawn, 15000, null, maintainPosture: true, maintainSleep: true);
                    }
                }
            };

            toil.tickAction = delegate
            {
                var pawn = toil.actor;
                progress += ProgressByTick;

                if (progress >= WorkTotal)
                {
                    OnJobCompleted();
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
            };

            toil.AddFinishAction(delegate
            {
                var targetPawn = MainTargetThing as Pawn;
                if (targetPawn != null && targetPawn.CurJobDef == JobDefOf.Wait_MaintainPosture)
                {
                    targetPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            });

            toil.FailOnDespawnedOrNull(MainTargetIndex);
            toil.FailOnCannotTouch(MainTargetIndex, PathEndMode.Touch);
            toil.AddEndCondition(() => CanProgressJob ? JobCondition.Ongoing : JobCondition.Incompletable);
            toil.WithProgressBar(MainTargetIndex, () => progress / WorkTotal);

            yield return toil;
        }
    }
}
