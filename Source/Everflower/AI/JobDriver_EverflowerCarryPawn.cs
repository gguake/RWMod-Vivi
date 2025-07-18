using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_EverflowerCarryPawn : JobDriver
    {
        private const TargetIndex EverflowerIndex = TargetIndex.A;
        private const TargetIndex TargetPawnIndex = TargetIndex.B;

        private ArcanePlant_Everflower Everflower => (ArcanePlant_Everflower)job.GetTarget(EverflowerIndex).Thing;
        private Pawn TargetPawn => job.GetTarget(TargetPawnIndex).Pawn;

        private float _workDone;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _workDone, "workDone");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(Everflower, job, 1, 1, null, errorOnFailed, ignoreOtherReservations: true))
            {
                if (pawn.Reserve(TargetPawn, job, 1, 1, null, errorOnFailed))
                {
                    return true;
                }
            }

            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var ritualDef = Everflower?.CurReservationInfo?.ritualDef;
            this.FailOnDestroyedNullOrForbidden(EverflowerIndex);
            this.FailOnDestroyedNullOrForbidden(TargetPawnIndex);
            this.FailOn(() => ritualDef == null || Everflower.CurReservationInfo == null || Everflower.CurReservationInfo.casterPawn != pawn || !ritualDef.Worker.CanRitual(Everflower, pawn));

            yield return Toils_Goto.GotoThing(TargetPawnIndex, PathEndMode.Touch);

            yield return Toils_Haul.StartCarryThing(TargetPawnIndex);
            yield return Toils_Goto.GotoThing(EverflowerIndex, PathEndMode.Touch);

            var workAmount = ritualDef?.jobWorkAmount ?? 1;
            var toil = ToilMaker.MakeToil("JobDriver_EverflowerCarryPawn_Work")
                .WithInitAction(() =>
                {
                    pawn.pather.StopDead();
                })
                .WithTickIntervalAction((delta) =>
                {
                    _workDone += pawn.GetStatValue(StatDefOf.PsychicSensitivity) * delta;
                    if (_workDone >= workAmount)
                    {
                        ReadyForNextToil();
                    }
                })
                .WithProgressBar(TargetPawnIndex, () => _workDone / workAmount, interpolateBetweenActorAndTarget: true)
                .WithDefaultCompleteMode(ToilCompleteMode.Never);

            if (ritualDef?.soundOnCasting != null)
            {
                toil = toil.PlaySustainerOrSound(ritualDef.soundOnCasting);
            }

            if (ritualDef?.effectOnCasting != null)
            {
                toil = toil.WithEffect(ritualDef.effectOnCasting, EverflowerIndex);
            }

            if (ritualDef?.targetEffectOnCasting != null)
            {
                toil = toil.WithEffect(ritualDef.targetEffectOnCasting, TargetPawnIndex);
            }

            yield return toil;

            yield return Toils_General.DoAtomic(() =>
            {
                ritualDef.Worker.Complete(Everflower.CurReservationInfo);
            });
        }
    }
}
