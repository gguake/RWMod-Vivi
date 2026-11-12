using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_FairyInterference : HediffWithComps
    {
        public const float ReleaseRadius = 2.9f;
        public static readonly IntRange ReturnDelayTicks = new IntRange(1500, 2500);

        private const int BossSearchIntervalTicks = 30;

        private static readonly Vector3 ShimmerOffset = new Vector3(0f, 0f, 0.12f);

        [Unsaved]
        private Effecter _auraEffecter;

        [Unsaved]
        private Mote _shimmerMote;

        [Unsaved]
        private Pawn _boss;

        public override string TipStringExtra
        {
            get
            {
                var tip = base.TipStringExtra;
                var disappears = GetComp<HediffComp_Disappears>();
                if (disappears == null || disappears.ticksToDisappear <= 0) { return tip; }

                var remaining = LocalizeString_Etc.VV_Hediff_FairyInterferenceRemaining.Translate(disappears.ticksToDisappear.ToStringTicksToPeriod()).Resolve();
                return tip.NullOrEmpty() ? remaining : tip + "\n" + remaining;
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                EndAura();
                return;
            }

            MaintainAura();

            if (pawn.Downed) { return; }

            var boss = GetBoss();
            if (boss == null || !ApocryphaGimmicks.IsBoundBoss(boss, out var comp)) { return; }
            if (!ApocryphaGimmicks.IsBossFightActive) { return; }

            if (pawn.Position.InHorDistOf(boss.Position, ReleaseRadius))
            {
                TriggerManaBackflow(boss, comp);
            }
        }

        private void TriggerManaBackflow(Pawn boss, CompApocrypha comp)
        {
            var vivi = pawn;

            EndAura();
            vivi.health.RemoveHediff(this);

            ApocryphaGimmicks.PlayManaBackflow(vivi, boss);
            comp.ReleaseBond(vivi);

            DoomBlightUtility.ClearDoomBlights(vivi);
            Messages.Message(
                LocalizeString_Message.VV_Message_FairyBondReleased.Translate(vivi.Named("PAWN")),
                new LookTargets(boss),
                MessageTypeDefOf.PositiveEvent);

            var anchor = ViviEndingQuest.Current?.AnchorEverflower;
            if (anchor != null && !anchor.Destroyed && vivi.Spawned && !vivi.Dead)
            {
                anchor.PawnHolder.HoldUntilReturn(vivi, ReturnDelayTicks.RandomInRange);
            }
        }

        private Pawn GetBoss()
        {
            if (_boss != null && _boss.Spawned && _boss.Map == pawn.Map) { return _boss; }

            var questBoss = ViviEndingQuest.Current?.Boss;
            if (questBoss != null && questBoss.Spawned && questBoss.Map == pawn.Map)
            {
                _boss = questBoss;
            }
            else if (pawn.IsHashIntervalTick(BossSearchIntervalTicks))
            {
                _boss = ApocryphaGimmicks.FindBoss(pawn.Map);
            }
            else
            {
                _boss = null;
            }

            return _boss;
        }

        private void MaintainAura()
        {
            var auraDef = VVEffecterDefOf.VV_Effecter_FairyInterferenceAura;
            if (auraDef == null) { return; }

            if (_auraEffecter == null)
            {
                _auraEffecter = auraDef.SpawnAttached(pawn, pawn.Map);
            }

            _auraEffecter.EffectTick(new TargetInfo(pawn), TargetInfo.Invalid);

            if (_shimmerMote == null || _shimmerMote.Destroyed)
            {
                _shimmerMote = MoteMaker.MakeAttachedOverlay(pawn, VVThingDefOf.VV_Mote_FairyInterferenceShimmer, ShimmerOffset);
            }

            _shimmerMote.Maintain();
        }

        private void EndAura()
        {
            _shimmerMote = null;

            if (_auraEffecter == null) { return; }

            _auraEffecter.Cleanup();
            _auraEffecter = null;
        }

        public override void PostRemoved()
        {
            EndAura();
            base.PostRemoved();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            EndAura();

            if (pawn?.health != null && pawn.health.hediffSet.hediffs.Contains(this))
            {
                pawn.health.RemoveHediff(this);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (!DebugSettings.godMode || pawn == null || !pawn.Spawned) { yield break; }

            var boss = ApocryphaGimmicks.FindBoss(pawn.Map);
            var bossBound = ApocryphaGimmicks.IsBoundBoss(boss);

            var commandRelease = new Command_Action()
            {
                defaultLabel = "DEV: Release bond now",
                action = () =>
                {
                    var target = ApocryphaGimmicks.FindBoss(pawn.Map);
                    if (ApocryphaGimmicks.IsBoundBoss(target, out var targetComp) && pawn.health.hediffSet.hediffs.Contains(this))
                    {
                        TriggerManaBackflow(target, targetComp);
                    }
                },
            };
            if (!bossBound)
            {
                commandRelease.Disable("No bound Asphodel on this map.");
            }
            yield return commandRelease;

            var commandTeleport = new Command_Action()
            {
                defaultLabel = "DEV: Teleport next to Asphodel",
                action = () =>
                {
                    var target = ApocryphaGimmicks.FindBoss(pawn.Map);
                    if (target == null) { return; }

                    var map = target.Map;
                    if (CellFinder.TryFindRandomCellNear(target.Position, map, 2, c => c != target.Position && c.Standable(map) && c.InHorDistOf(target.Position, 2f), out var cell))
                    {
                        pawn.Position = cell;
                        pawn.Notify_Teleported();
                    }
                },
            };
            if (boss == null)
            {
                commandTeleport.Disable("No Asphodel on this map.");
            }
            yield return commandTeleport;
        }
    }
}
