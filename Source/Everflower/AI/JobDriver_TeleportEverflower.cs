using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_TeleportEverflower : JobDriver
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
            return pawn.Reserve(Everflower, job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(EverflowerIndex);
            this.FailOn(() => !Everflower.ReservedTeleportCell.HasValue);

            yield return Toils_Goto.GotoThing(EverflowerIndex, PathEndMode.Touch);

            var workAmount = 3000;
            var toil = ToilMaker.MakeToil()
                .WithTickIntervalAction((delta) =>
                {
                    _workDone += pawn.GetStatValue(StatDefOf.PsychicSensitivity) * delta;
                    if (_workDone >= workAmount)
                    {
                        ReadyForNextToil();
                    }
                })
                .WithProgressBar(EverflowerIndex, () => _workDone / workAmount, interpolateBetweenActorAndTarget: true)
                .WithDefaultCompleteMode(ToilCompleteMode.Never)
                .WithEffect(VVEffecterDefOf.VV_EverflowerLink, EverflowerIndex);

            yield return toil;

            yield return Toils_General.Do(() =>
            {
                Everflower.Notify_TeleportJobCompleted();
            });
        }
    }
}
