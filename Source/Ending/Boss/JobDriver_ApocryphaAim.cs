using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VVRace
{
    // 2페이즈 독성 브레스 준비(VV_ApocryphaBreath) 동안 제자리에서 대상 칸을 바라보는 작업. 시간 판정과 실제 방출은 CompApocrypha가 한다.
    // 1페이즈 사출 포격 조준(VV_ApocryphaAim)은 이동을 멈추지 않도록 CompApocrypha가 작업 없이 처리하므로, 그 작업은 불러온 옛 세이브에서만 남아 있다가 바로 끝난다.
    public class JobDriver_ApocryphaAim : JobDriver
    {
        private const TargetIndex CellIndex = TargetIndex.A;
        private const int BreathDroolInterval = 5;

        private bool IsBreath => job.def == ApocryphaJobDefOf.VV_ApocryphaBreath;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var toil = ToilMaker.MakeToil("ApocryphaAim")
                .WithInitAction(() =>
                {
                    if (!IsBreath)
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }

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

        // 브레스 준비: 입에서 독액 섞인 가스가 흘러내린다.
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
