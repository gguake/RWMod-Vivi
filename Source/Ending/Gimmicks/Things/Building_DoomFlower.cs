using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class Building_DoomFlower : Building_ApocryphaFlower, IAttackTarget
    {
        public const int BloomDelayTicks = 30000;

        private int _bloomTick = -1;

        public static int ScaledBloomDelayTicks => Mathf.Max(1, Mathf.RoundToInt(BloomDelayTicks * CompApocrypha.DoomBloomTimeFactor));

        public int TicksUntilBloom => _bloomTick < 0 ? ScaledBloomDelayTicks : System.Math.Max(0, _bloomTick - Find.TickManager.TicksGame);

        #region IAttackTarget

        Thing IAttackTarget.Thing => this;

        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

        public float TargetPriorityFactor => 0.5f;

        // 호위 말벌과 보스가 꽃을 공격하지 않도록 플레이어·우호 세력의 검색자에게만 위협으로 취급한다.
        public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
        {
            if (!Spawned) { return true; }

            var searcherFaction = disabledFor?.Thing?.Faction;
            if (searcherFaction == null || searcherFaction.HostileTo(Faction.OfPlayer)) { return true; }

            // 지붕 붕괴와 실내 폭발로 정착지를 스스로 포격하지 않도록 지붕 아래 꽃은 박격포류의 자동 조준에서 제외한다.
            return Position.Roofed(Map) && (disabledFor.CurrentEffectiveVerb?.ProjectileFliesOverhead() ?? false);
        }

        #endregion

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (_bloomTick < 0)
            {
                _bloomTick = Find.TickManager.TicksGame + ScaledBloomDelayTicks;
            }
        }

        protected override void Tick()
        {
            base.Tick();

            if (Spawned && _bloomTick >= 0 && Find.TickManager.TicksGame >= _bloomTick)
            {
                Bloom();
            }
        }

        public void Bloom()
        {
            if (!Spawned) { return; }

            var map = Map;
            var cell = Position;

            if (!ApocryphaGimmicks.IsGimmickActive(ApocryphaGimmicks.FindBoss(map)))
            {
                Wither();
                return;
            }

            ApocryphaGimmicks.PlayEffect(VVEffecterDefOf.VV_Effecter_DoomBloomCurse, cell, map);
            DoomBlightUtility.ApplyDoomBlight(map, cell);
            Wither();
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            if (sb.Length > 0) { sb.AppendLine(); }

            sb.Append(LocalizeString_Inspector.VV_Inspect_DoomFlowerBloomIn.Translate(TicksUntilBloom.ToStringTicksToPeriod()));
            sb.AppendLine();
            sb.Append(LocalizeString_Inspector.VV_Inspect_DoomFlowerHint.Translate());
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (DebugSettings.godMode)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Bloom now",
                    action = Bloom,
                };

                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Wither now",
                    action = Wither,
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _bloomTick, "bloomTick", -1);
        }
    }
}
