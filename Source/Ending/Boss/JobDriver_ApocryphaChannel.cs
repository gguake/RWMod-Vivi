using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 영원꽃 찬탈 채널링. 진행도는 CompApocrypha가 보관하고(경고/정보창이 읽음) 이 작업은 매 틱 진행과 중단 판정만 한다.
    // 기절은 작업 틱을 멈추지 않으므로 매 틱 직접 확인해 진행도를 0으로 되돌린다.
    public class JobDriver_ApocryphaChannel : JobDriver
    {
        private const TargetIndex StealTargetIndex = TargetIndex.A;
        private const TargetIndex EverflowerIndex = TargetIndex.B;
        private const float VortexRotationPerTick = -2.4f;

        private Effecter _effecter;
        private Mote _vortexMote;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(StealTargetIndex);

            var toil = ToilMaker.MakeToil("ApocryphaChannel")
                .WithInitAction(() =>
                {
                    pawn.pather?.StopDead();
                    pawn.rotationTracker.FaceTarget(job.GetTarget(StealTargetIndex));
                })
                .WithTickAction(() =>
                {
                    var comp = pawn.TryGetComp<CompApocrypha>();
                    if (comp == null || !comp.IsActive)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    // 기절하면 채널링이 중단되고 진행도는 처음부터 다시 쌓아야 한다.
                    if (pawn.stances.stunner.Stunned)
                    {
                        comp.InterruptChannel();
                        return;
                    }

                    if (!comp.InStealRange())
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    pawn.rotationTracker.FaceTarget(job.GetTarget(StealTargetIndex));
                    comp.Notify_ChannelTick();
                    TickVisuals();
                })
                .WithFinishAction(() =>
                {
                    pawn.TryGetComp<CompApocrypha>()?.Notify_ChannelJobEnded();
                    _effecter?.Cleanup();
                    _effecter = null;
                    _vortexMote = null;
                })
                .WithDefaultCompleteMode(ToilCompleteMode.Never)
                .WithHandlingFacing();

            yield return toil;
        }

        // 영원꽃 아래에서 자홍빛 소용돌이가 돌며 생명력이 보스 쪽으로 빨려 들어간다(연결 빔은 CompApocrypha.PostDraw가 그린다).
        private void TickVisuals()
        {
            var map = pawn.Map;
            var everflower = job.GetTarget(EverflowerIndex).Thing ?? job.GetTarget(StealTargetIndex).Thing;
            if (everflower == null || !everflower.Spawned) { return; }

            if (_vortexMote == null || _vortexMote.Destroyed)
            {
                _vortexMote = MoteMaker.MakeStaticMote(everflower.DrawPos, map, ApocryphaThingDefOf.VV_Mote_ApocryphaDrainVortex, 1f, makeOffscreen: true, exactRot: Rand.Range(0f, 360f));
            }

            if (_vortexMote != null)
            {
                _vortexMote.exactPosition = everflower.DrawPos;
                _vortexMote.exactRotation = Mathf.Repeat(_vortexMote.exactRotation + VortexRotationPerTick, 360f);
                _vortexMote.Maintain();
            }

            var source = new TargetInfo(everflower);
            var target = new TargetInfo(pawn);
            if (_effecter == null)
            {
                _effecter = ApocryphaEffecterDefOf.VV_Effecter_ApocryphaChannel.Spawn();
                _effecter.Trigger(source, target);
            }

            _effecter.EffectTick(source, target);
        }
    }
}
