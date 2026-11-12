using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
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

                    if (pawn.stances.stunner.Stunned)
                    {
                        comp.Channel.Interrupt();
                        return;
                    }

                    if (!comp.Channel.InStealRange())
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    pawn.rotationTracker.FaceTarget(job.GetTarget(StealTargetIndex));
                    comp.Channel.Notify_ChannelTick();
                    TickVisuals();
                })
                .WithFinishAction(() =>
                {
                    pawn.TryGetComp<CompApocrypha>()?.Channel.Notify_ChannelJobEnded();
                    _effecter?.Cleanup();
                    _effecter = null;
                    _vortexMote = null;
                })
                .WithDefaultCompleteMode(ToilCompleteMode.Never)
                .WithHandlingFacing();

            yield return toil;
        }

        private void TickVisuals()
        {
            var map = pawn.Map;
            var everflower = job.GetTarget(EverflowerIndex).Thing ?? job.GetTarget(StealTargetIndex).Thing;
            if (everflower == null || !everflower.Spawned) { return; }

            if (_vortexMote == null || _vortexMote.Destroyed)
            {
                _vortexMote = MoteMaker.MakeStaticMote(everflower.DrawPos, map, VVThingDefOf.VV_Mote_ApocryphaDrainVortex, 1f, makeOffscreen: true, exactRot: Rand.Range(0f, 360f));
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
                _effecter = VVEffecterDefOf.VV_Effecter_ApocryphaChannel.Spawn();
                _effecter.Trigger(source, target);
            }

            _effecter.EffectTick(source, target);
        }
    }
}
