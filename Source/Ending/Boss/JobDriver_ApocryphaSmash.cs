using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VVRace
{
    public class JobDriver_ApocryphaSmash : JobDriver
    {
        private const TargetIndex BlockerIndex = TargetIndex.A;
        private const TargetIndex StandCellIndex = TargetIndex.B;

        private int _smashTicks;

        private Thing Blocker => job.GetTarget(BlockerIndex).Thing;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _smashTicks, "smashTicks", 0);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(BlockerIndex);
            this.FailOn(() =>
            {
                var comp = pawn.TryGetComp<CompApocrypha>();
                return comp == null || comp.Phase != ApocryphaPhase.Unbound;
            });

            yield return Toils_Goto.GotoCell(StandCellIndex, PathEndMode.OnCell);

            var smash = ToilMaker.MakeToil("ApocryphaSmash")
                .WithInitAction(() =>
                {
                    pawn.pather?.StopDead();
                    _smashTicks = 0;
                })
                .WithTickAction(() =>
                {
                    var blocker = Blocker;
                    if (blocker == null || blocker.Destroyed)
                    {
                        ReadyForNextToil();
                        return;
                    }

                    pawn.rotationTracker.FaceTarget(blocker);
                    if (pawn.stances.stunner.Stunned) { return; }

                    var comp = pawn.TryGetComp<CompApocrypha>();
                    if (comp == null) { return; }

                    if (++_smashTicks % Mathf.Max(1, comp.Props.smashIntervalTicks) == 0)
                    {
                        SmashOnce(blocker, comp.Props.smashDamage);
                    }
                })
                .WithDefaultCompleteMode(ToilCompleteMode.Never)
                .WithHandlingFacing();

            yield return smash;
        }

        private void SmashOnce(Thing blocker, float damage)
        {
            var map = pawn.Map;
            var cell = blocker.Position;
            var angle = (blocker.Position - pawn.Position).AngleFlat;

            ApocryphaEffecterDefOf.VV_Effecter_ApocryphaSmash.Spawn(cell, map).Cleanup();
            pawn.RaceProps.soundMeleeHitBuilding?.PlayOneShot(new TargetInfo(cell, map));
            ApocryphaUtility.ShakeCamera(map, 0.35f);

            blocker.TakeDamage(new DamageInfo(DamageDefOf.Crush, damage, 1f, angle, pawn));
        }
    }
}
