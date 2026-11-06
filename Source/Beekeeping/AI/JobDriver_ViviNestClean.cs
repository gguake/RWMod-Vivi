using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 바닐라 청소 잡(JobDriver_CleanFilth)과 같은 순서·작업량. 바닐라 것은 집 구역 밖 오물을 건너뛰는데(JumpIfOutsideHomeArea) 둥지 방은 플레이어 집 구역이 아니라서 그 검사만 뺐다.
    public class JobDriver_ViviNestClean : JobDriver
    {
        private float _cleaningWorkDone;
        private float _totalCleaningWorkDone;
        private float _totalCleaningWorkRequired;

        private Filth Filth => (Filth)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.A), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var nextTarget = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A);
            yield return nextTarget;
            yield return Toils_JobTransforms.SucceedOnNoTargetInQueue(TargetIndex.A);
            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, nextTarget);

            var clean = ToilMaker.MakeToil("VV_ViviNestClean");
            clean.initAction = () =>
            {
                _cleaningWorkDone = 0f;
                _totalCleaningWorkDone = 0f;
                _totalCleaningWorkRequired = Filth.def.filth.cleaningWorkToReduceThickness * Filth.thickness;
            };
            clean.tickIntervalAction = delta =>
            {
                var filth = Filth;
                var timeFactor = filth.Position.GetTerrain(filth.Map).GetStatValueAbstract(StatDefOf.CleaningTimeFactor);
                var work = pawn.GetStatValue(StatDefOf.CleaningSpeed) * delta;
                if (timeFactor != 0f) { work /= timeFactor; }

                _cleaningWorkDone += work;
                _totalCleaningWorkDone += work;
                if (_cleaningWorkDone > filth.def.filth.cleaningWorkToReduceThickness)
                {
                    filth.ThinFilth();
                    _cleaningWorkDone = 0f;
                    if (filth.Destroyed)
                    {
                        clean.actor.records.Increment(RecordDefOf.MessesCleaned);
                        ReadyForNextToil();
                    }
                }
            };
            clean.defaultCompleteMode = ToilCompleteMode.Never;
            clean.WithEffect(EffecterDefOf.Clean, TargetIndex.A);
            clean.WithProgressBar(TargetIndex.A, () => _totalCleaningWorkDone / _totalCleaningWorkRequired, interpolateBetweenActorAndTarget: true);
            clean.PlaySustainerOrSound(() => Filth.def.filth.cleaningSound.NullOrUndefined() ? SoundDefOf.Interact_CleanFilth : Filth.def.filth.cleaningSound);
            clean.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, nextTarget);
            clean.JumpIf(() => clean.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing?.Destroyed ?? false, nextTarget);
            yield return clean;
            yield return Toils_Jump.Jump(nextTarget);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _cleaningWorkDone, "cleaningWorkDone");
            Scribe_Values.Look(ref _totalCleaningWorkDone, "totalCleaningWorkDone");
            Scribe_Values.Look(ref _totalCleaningWorkRequired, "totalCleaningWorkRequired");
        }
    }
}
