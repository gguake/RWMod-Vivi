using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 멸망꽃. 피어난 지 12시간(30000틱)이 지나면 만개해 맵의 모든 정착민에게 멸망꽃 저주를 내리고 시든다.
    // 보스 세력 소속이라 적대 표시가 되며, 플레이어 세력(포탑·징집된 정착민)만 자동 공격 대상으로 삼는다(지붕 아래 꽃은 박격포류가 자동 조준하지 않음).
    public class Building_DoomFlower : Building_ApocryphaFlower, IAttackTarget
    {
        public const int BloomDelayTicks = 30000;

        private int _bloomTick = -1;

        public int TicksUntilBloom => _bloomTick < 0 ? BloomDelayTicks : System.Math.Max(0, _bloomTick - Find.TickManager.TicksGame);

        #region IAttackTarget

        Thing IAttackTarget.Thing => this;

        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

        // 실제로 공격해 오는 적보다는 덜 우선한다.
        public float TargetPriorityFactor => 0.5f;

        // 플레이어·우호 세력의 포탑/폰만 자동 공격한다. 무소속(호위 거대 말벌)·적대 세력(보스)은 노리지 않아,
        // 말벌이 보스가 피운 꽃을 부수는 일이 없다. (null 검색자는 위협으로 세지 않는다 — 드롭 위치 계산 등)
        public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
        {
            if (!Spawned) { return true; }

            var searcherFaction = disabledFor?.Thing?.Faction;
            if (searcherFaction == null || searcherFaction.HostileTo(Faction.OfPlayer)) { return true; }

            // 박격포처럼 머리 위로 쏘는 무기는 지붕 아래(실내)에 핀 꽃을 자동 조준하지 않는다.
            // 포탄이 지붕을 무너뜨리고 방 안에서 터져 정착지를 스스로 포격하게 되기 때문(직접 지정한 공격은 그대로 허용).
            return Position.Roofed(Map) && (disabledFor.CurrentEffectiveVerb?.ProjectileFliesOverhead() ?? false);
        }

        #endregion

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (_bloomTick < 0)
            {
                _bloomTick = Find.TickManager.TicksGame + BloomDelayTicks;
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

        // 만개: 옛 제국의 멸망을 재현하는 저주를 내리고 시든다.
        // 보스 단계가 아니거나 이 맵의 아포크리파가 1페이즈(결속 상태)가 아니면 저주 없이 시들기만 한다.
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

            ApocryphaGimmicks.PlayEffect(ApocryphaGimmickDefOf.VV_Effecter_DoomBloomCurse, cell, map);
            DoomBlightUtility.ApplyDoomBlight(map, cell);
            Wither();
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            if (sb.Length > 0) { sb.AppendLine(); }

            sb.Append(LocalizeString_EndingGimmicks.VV_Inspect_DoomFlowerBloomIn.Translate(TicksUntilBloom.ToStringTicksToPeriod()));
            sb.AppendLine();
            sb.Append(LocalizeString_EndingGimmicks.VV_Inspect_DoomFlowerHint.Translate());
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
