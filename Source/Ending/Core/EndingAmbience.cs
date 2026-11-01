using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public partial class ViviEndingQuest
    {
        private static readonly IntRange AltarPulseIntervalTicks = new IntRange(420, 660);

        private const float EndingFlashFadeInSeconds = 0.6f;
        private const float EndingFlashHoldSeconds = 0.67f;     // 1배속 40틱에 해당하는 실시간 유지 시간. 일시정지 중에도 흐른다.
        private const float EndingFlashFadeOutSeconds = 3f;

        private static readonly Color EndingFlashColor = new Color(1f, 0.85f, 0.45f, 0.55f);

        private int _nextAltarPulseTick = -1;

        private float _fadeBackRealtime = -1f;

        private void TickAltarPulse(int now)
        {
            if (!IsEndingActive)
            {
                _nextAltarPulseTick = -1;
                return;
            }

            if (_nextAltarPulseTick < 0)
            {
                _nextAltarPulseTick = now + AltarPulseIntervalTicks.RandomInRange;
                return;
            }

            if (now < _nextAltarPulseTick) { return; }

            _nextAltarPulseTick = now + AltarPulseIntervalTicks.RandomInRange;
            PlayAltarPulse();
        }

        private void PlayAltarPulse()
        {
            var anchor = _anchor;
            if (anchor == null || !anchor.Spawned || anchor.Map != Find.CurrentMap) { return; }

            var comp = anchor.EverflowerComp;
            if (comp == null || !comp.IsEnshrined) { return; }

            // 이펙트는 제단 위 5·6단계 그래픽 기준이므로 그래픽이 다른 낮은 단계에서는 재생하지 않는다.
            var effecterDef = comp.AttunementLevel >= 6 ? EndingCoreDefOf.VV_Effecter_AltarEverflowerPulse_6
                : comp.AttunementLevel == 5 ? EndingCoreDefOf.VV_Effecter_AltarEverflowerPulse_5
                : null;

            effecterDef?.Spawn(anchor.Position, anchor.Map).Cleanup();
        }

        public void PlayEndingFlash()
        {
            ScreenFader.StartFade(EndingFlashColor, EndingFlashFadeInSeconds);
            _fadeBackRealtime = Time.realtimeSinceStartup + EndingFlashHoldSeconds;
        }

        private void UpdateEndingScreenFade()
        {
            if (_fadeBackRealtime < 0f || Time.realtimeSinceStartup < _fadeBackRealtime) { return; }

            _fadeBackRealtime = -1f;
            ScreenFader.StartFade(Color.clear, EndingFlashFadeOutSeconds);
        }
    }
}
