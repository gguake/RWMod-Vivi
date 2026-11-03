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
        private int _nextHornetTick = -1;

        // 난이도 배율은 보스전 시작 시점에 보스가 고정한 값을 쓴다.
        private CompApocrypha BossComp => ApocryphaGimmicks.FindBoss(Map)?.TryGetComp<CompApocrypha>();

        private int ScaledBloomDelayTicks => Mathf.Max(1, Mathf.RoundToInt(BloomDelayTicks * (BossComp?.DoomBloomTimeFactor ?? 1f)));

        // 만개 지연과 같은 난이도 배율을 쓴다.
        private int ScaledHornetIntervalTicks
        {
            get
            {
                var comp = BossComp;
                return Mathf.Max(1, Mathf.RoundToInt((comp?.Props.doomFlowerHornetIntervalTicks ?? 10000) * (comp?.DoomBloomTimeFactor ?? 1f)));
            }
        }

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

            if (_nextHornetTick < 0)
            {
                _nextHornetTick = Find.TickManager.TicksGame + ScaledHornetIntervalTicks;
            }
        }

        protected override void Tick()
        {
            base.Tick();

            if (!Spawned) { return; }

            var now = Find.TickManager.TicksGame;
            if (_bloomTick >= 0 && now >= _bloomTick)
            {
                Bloom();
                return;
            }

            if (_nextHornetTick >= 0 && now >= _nextHornetTick)
            {
                _nextHornetTick = now + ScaledHornetIntervalTicks;

                // 말벌이 빚어지는 사이 꽃이 만개해 사라지면 지킬 꽃 없이 나오므로 만개 직전 주기는 건너뛴다.
                if (_bloomTick < 0 || now + HornetManifestation.DurationTicks < _bloomTick)
                {
                    TrySummonHornets();
                }
            }
        }

        // 상처 입은 꽃은 소환 주기를 건너뛴다.
        public void TrySummonHornets()
        {
            if (!Spawned || HitPoints < MaxHitPoints) { return; }

            var boss = ApocryphaGimmicks.FindBoss(Map);
            if (!ApocryphaGimmicks.IsGimmickActive(boss)) { return; }

            var count = boss.TryGetComp<CompApocrypha>()?.Props.doomFlowerHornetCount ?? 1;
            HornetManifestation.SummonFromFlower(this, boss, count);
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

                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Summon hornet now",
                    action = TrySummonHornets,
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _bloomTick, "bloomTick", -1);
            Scribe_Values.Look(ref _nextHornetTick, "nextHornetTick", -1);
        }
    }
}
