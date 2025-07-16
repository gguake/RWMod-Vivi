using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_Fairyfication : JobDriver
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
            this.FailOnDestroyedNullOrForbidden(EverflowerIndex);
            this.FailOnDestroyedNullOrForbidden(TargetPawnIndex);
            this.FailOn(() => Everflower.CurReservedPawn != pawn);

            yield return Toils_Goto.GotoThing(TargetPawnIndex, PathEndMode.Touch);

            yield return Toils_Haul.StartCarryThing(TargetPawnIndex);
            yield return Toils_Goto.GotoThing(EverflowerIndex, PathEndMode.Touch);

            var ticks = (int)(Everflower.CurReservedRitual.jobWorkAmount / pawn.GetStatValue(StatDefOf.PsychicSensitivity));
            yield return ToilMaker.MakeToil("JobDriver_Fairyfication_Work")
                .WithInitAction(() =>
                {
                    pawn.pather.StopDead();
                })
                .WithTickIntervalAction((delta) =>
                {
                    _workDone += pawn.GetStatValue(StatDefOf.PsychicSensitivity) * delta;
                    if (_workDone >= Everflower.CurReservedRitual.jobWorkAmount)
                    {
                        ReadyForNextToil();
                    }
                })
                .WithProgressBar(TargetPawnIndex, () => _workDone / Everflower.CurReservedRitual.jobWorkAmount, interpolateBetweenActorAndTarget: true)
                .WithEffect(() => VVEffecterDefOf.VV_EverflowerLink, TargetPawnIndex)
                //.PlaySustainerOrSound()
                .WithDefaultCompleteMode(ToilCompleteMode.Never);

            yield return Toils_General.DoAtomic(() =>
            {
                if (pawn.TryGetComp<CompViviHolder>(out var compViviHolder))
                {
                    compViviHolder.JoinVivi(TargetPawn);
                }

                Everflower.CurReservedRitual.Worker.Complete(Everflower, pawn);
            });
        }
    }
}
