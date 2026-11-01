using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VVRace
{
    public class JobDriver_ApocryphaBreath : JobDriver
    {
        private const TargetIndex CellIndex = TargetIndex.A;
        private const int BreathDroolInterval = 5;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var toil = ToilMaker.MakeToil("ApocryphaBreath")
                .WithInitAction(() =>
                {
                    pawn.pather?.StopDead();
                    pawn.rotationTracker.FaceCell(job.GetTarget(CellIndex).Cell);
                    pawn.ageTracker.CurLifeStageRace?.soundAngry?.PlayOneShot(new TargetInfo(pawn));
                })
                .WithTickAction(() =>
                {
                    var comp = pawn.TryGetComp<CompApocrypha>();
                    if (comp == null || !comp.IsBreathing)
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }

                    pawn.rotationTracker.FaceCell(job.GetTarget(CellIndex).Cell);
                    TickBreathVisuals(comp);
                })
                .WithDefaultCompleteMode(ToilCompleteMode.Never)
                .WithHandlingFacing();

            yield return toil;
        }

        private void TickBreathVisuals(CompApocrypha comp)
        {
            if (!pawn.IsHashIntervalTick(BreathDroolInterval)) { return; }

            var map = pawn.Map;
            var mouth = pawn.DrawPos + comp.MouthOffset();
            if (!mouth.ShouldSpawnMotesAt(map)) { return; }

            var data = FleckMaker.GetDataStatic(mouth + Gen.RandomHorizontalVector(0.35f), map, ApocryphaFleckDefOf.VV_Fleck_ApocryphaToxicGas, Rand.Range(0.6f, 1.1f));
            data.velocityAngle = Rand.Range(150f, 210f);
            data.velocitySpeed = Rand.Range(0.3f, 0.8f);
            data.rotation = Rand.Range(0f, 360f);
            data.rotationRate = Rand.Range(-40f, 40f);
            map.flecks.CreateFleck(data);
        }
    }
}
