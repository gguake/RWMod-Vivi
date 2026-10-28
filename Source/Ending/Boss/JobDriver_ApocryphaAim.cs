using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VVRace
{
    // 요정 비비 사출 포격 조준(VV_ApocryphaAim) / 독성 브레스 준비(VV_ApocryphaBreath) 동안 제자리에서 대상 칸을 바라보는 작업.
    // 시간 판정과 실제 발사는 CompApocrypha가 하고, 이 작업은 자세와 경고 연출만 맡는다(모트는 저장되지 않으므로 불러온 뒤 다시 만든다).
    public class JobDriver_ApocryphaAim : JobDriver
    {
        private const TargetIndex CellIndex = TargetIndex.A;
        private const int BreathDroolInterval = 5;

        private Mote _targetMote;
        private Mote _chargeMote;
        private Effecter _effecter;

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
                    pawn.pather?.StopDead();
                    pawn.rotationTracker.FaceCell(job.GetTarget(CellIndex).Cell);

                    if (IsBreath)
                    {
                        pawn.ageTracker.CurLifeStageRace?.soundAngry?.PlayOneShot(new TargetInfo(pawn));
                    }
                })
                .WithTickAction(() =>
                {
                    var comp = pawn.TryGetComp<CompApocrypha>();
                    if (comp == null || !(IsBreath ? comp.IsBreathing : comp.IsAiming))
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }

                    pawn.rotationTracker.FaceCell(job.GetTarget(CellIndex).Cell);
                    if (IsBreath)
                    {
                        TickBreathVisuals(comp);
                    }
                    else
                    {
                        TickAimVisuals(comp);
                    }
                })
                .WithFinishAction(CleanupVisuals)
                .WithDefaultCompleteMode(ToilCompleteMode.Never)
                .WithHandlingFacing();

            yield return toil;
        }

        // 헬스피어 캐논식 금빛 조준 경고: 대상 칸의 표적 모트 + 보스 머리의 충전광 + 지속음.
        private void TickAimVisuals(CompApocrypha comp)
        {
            var map = pawn.Map;
            var cell = job.GetTarget(CellIndex).Cell;
            var stunned = pawn.stances.stunner.Stunned;

            var direction = cell.ToVector3Shifted() - pawn.DrawPos;
            direction.y = 0f;
            direction.Normalize();

            if (_targetMote == null || _targetMote.Destroyed)
            {
                _targetMote = MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, ApocryphaThingDefOf.VV_Mote_ApocryphaAimTarget, 1f, makeOffscreen: true);
            }

            if (_targetMote != null)
            {
                _targetMote.paused = stunned;
                _targetMote.exactRotation = direction.AngleFlat();
                _targetMote.Maintain();
            }

            var chargePos = comp.BodyCenter + direction * 1.4f;
            if (_chargeMote == null || _chargeMote.Destroyed)
            {
                _chargeMote = MoteMaker.MakeStaticMote(chargePos, map, ApocryphaThingDefOf.VV_Mote_ApocryphaAimCharge, 1f, makeOffscreen: true);
            }

            if (_chargeMote != null)
            {
                _chargeMote.paused = stunned;
                _chargeMote.exactPosition = chargePos;
                _chargeMote.exactRotation = direction.AngleFlat();
                _chargeMote.Maintain();
            }

            var targetInfo = new TargetInfo(cell, map);
            if (_effecter == null)
            {
                _effecter = ApocryphaEffecterDefOf.VV_Effecter_ApocryphaAim.Spawn();
                _effecter.Trigger(new TargetInfo(pawn), targetInfo);
            }

            _effecter.EffectTick(new TargetInfo(pawn), targetInfo);
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

        private void CleanupVisuals()
        {
            _effecter?.Cleanup();
            _effecter = null;
            _targetMote = null;
            _chargeMote = null;
        }
    }
}
