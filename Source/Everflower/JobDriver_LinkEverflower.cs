using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_LinkEverflower : JobDriver
    {
        public const float TotalWork = 2000f;

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
            this.FailOn(() => Everflower.ReservedPawn != pawn);

            yield return Toils_Goto.GotoThing(EverflowerIndex, PathEndMode.Touch);

            yield return ToilMaker.MakeToil("JobDriver_LinkEverflower_Work")
                .WithTickIntervalAction((delta) =>
                {
                    _workDone += pawn.GetStatValue(StatDefOf.PsychicSensitivity) * delta;
                    if (_workDone >= TotalWork)
                    {
                        ReadyForNextToil();
                    }
                })
                .WithProgressBar(EverflowerIndex, () => _workDone / TotalWork, interpolateBetweenActorAndTarget: true)
                .WithEffect(() => VVEffecterDefOf.VV_EverflowerLink, EverflowerIndex)
                //.PlaySustainerOrSound()
                .WithDefaultCompleteMode(ToilCompleteMode.Never);

            yield return Toils_General.Do(() =>
            {
                Everflower.Link(pawn);
            });
        }
    }
}
