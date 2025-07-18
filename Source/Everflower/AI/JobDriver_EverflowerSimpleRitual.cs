using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_EverflowerSimpleRitual : JobDriver
    {
        private const TargetIndex EverflowerIndex = TargetIndex.A;

        private ArcanePlant_Everflower Everflower => (ArcanePlant_Everflower)job.GetTarget(EverflowerIndex).Thing;

        private float _workDone;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _workDone, "workDone");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Everflower, job, 1, 1, null, errorOnFailed, ignoreOtherReservations: true);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var ritualDef = Everflower.CurReservationInfo.ritualDef;
            this.FailOnDestroyedNullOrForbidden(EverflowerIndex);
            this.FailOn(() => Everflower.CurReservationInfo == null || Everflower.CurReservationInfo.casterPawn != pawn || !Everflower.CurReservationInfo.ritualDef.Worker.CanRitual(Everflower, pawn));

            yield return Toils_Goto.GotoThing(EverflowerIndex, PathEndMode.Touch);

            var workAmount = ritualDef.jobWorkAmount;
            var toil = ToilMaker.MakeToil("JobDriver_AttunementEverflower_Work")
                .WithTickIntervalAction((delta) =>
                {
                    _workDone += pawn.GetStatValue(StatDefOf.PsychicSensitivity) * delta;
                    if (_workDone >= workAmount)
                    {
                        ReadyForNextToil();
                    }
                })
                .WithProgressBar(EverflowerIndex, () => _workDone / workAmount, interpolateBetweenActorAndTarget: true)
                //.PlaySustainerOrSound()
                .WithDefaultCompleteMode(ToilCompleteMode.Never);

            if (ritualDef.effectOnCasting != null)
            {
                toil = toil.WithEffect(ritualDef.effectOnCasting, EverflowerIndex);
            }

            yield return toil;

            yield return Toils_General.Do(() =>
            {
                Everflower.CurReservationInfo.ritualDef.Worker.Complete(Everflower.CurReservationInfo);
            });
        }
    }
}
