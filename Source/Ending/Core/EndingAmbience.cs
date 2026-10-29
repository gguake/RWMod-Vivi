using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 엔딩 분위기 연출: 엔딩 동안 제단 영원꽃의 주기적 발광, 엔딩 순간의 금빛 섬광(실시간으로 걷어냄).
    // 〈황금빛 꽃안개〉·〈불길한 정적〉 컨디션은 본편 퀘스트의 앵커 감시(QuestPart_ViviAnchorWatch)가 앵커가 있는 맵에 건다.
    public partial class ViviEndingQuest
    {
        private static readonly IntRange AltarPulseIntervalTicks = new IntRange(420, 660);

        private const float EndingFlashFadeInSeconds = 0.6f;
        private const float EndingFlashHoldSeconds = 0.67f;     // 40틱(1배속) 뒤 걷어낸다. 일시정지와 무관하게 실시간으로 처리한다.
        private const float EndingFlashFadeOutSeconds = 3f;

        private static readonly Color EndingFlashColor = new Color(1f, 0.85f, 0.45f, 0.55f);

        // 비저장: 다음 제단 발광 틱. 불러온 뒤에는 첫 틱에 다시 잡는다.
        private int _nextAltarPulseTick = -1;

        // 비저장: 금빛 섬광을 걷어낼 실시간(Time.realtimeSinceStartup). 음수면 대기 중인 섬광 없음.
        private float _fadeBackRealtime = -1f;

        // 매 틱: 엔딩이 진행되는 동안 제단에 안치된 앵커 영원꽃이 몇 초마다 은은하게 빛나며 꽃잎을 흩날린다(보고 있는 맵일 때만).
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

            // 이펙트 위치는 제단 위 5/6단계 그래픽의 꽃 기준이다(그보다 낮은 단계는 그래픽이 달라 재생하지 않는다).
            var effecterDef = comp.AttunementLevel >= 6 ? EndingCoreDefOf.VV_Effecter_AltarEverflowerPulse_6
                : comp.AttunementLevel == 5 ? EndingCoreDefOf.VV_Effecter_AltarEverflowerPulse_5
                : null;

            effecterDef?.Spawn(anchor.Position, anchor.Map).Cleanup();
        }

        // [엔딩 연출 호출] 엔딩 순간 화면을 금빛으로 물들였다가 잠시 뒤 걷어낸다.
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
