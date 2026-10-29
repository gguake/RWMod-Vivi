using RimWorld;
using Verse;

namespace VVRace
{
    // 엔딩 분위기 연출: 엔딩 동안 제단 영원꽃의 주기적 발광, 평화 기간부터 엔딩까지 이어지는 〈황금빛 꽃안개〉
    public partial class ViviEndingQuest
    {
        private static readonly IntRange AltarPulseIntervalTicks = new IntRange(420, 660);

        // 엔딩(대관식 완료)이 끝나면 황금빛 꽃안개는 이 시간 동안 남았다가 걷힌다.
        private const int GoldenBloomFadeTicks = 30000;

        // 비저장: 다음 제단 발광 틱. 불러온 뒤에는 첫 틱에 다시 잡는다.
        private int _nextAltarPulseTick = -1;

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

        // 평화 기간 시작: 앵커 맵에 〈황금빛 꽃안개〉를 엔딩이 끝날 때까지 건다.
        private void StartGoldenBloom()
        {
            var map = AnchorMap;
            if (map == null) { return; }

            var condition = GoldenBloomUtility.GetActiveCondition(map);
            if (condition == null)
            {
                condition = (GameCondition_GoldenBloom)GameConditionMaker.MakeCondition(EndingCoreDefOf.VV_GoldenBloom);
                condition.Permanent = true;
                condition.fromEnding = true;
                map.gameConditionManager.RegisterCondition(condition);
            }
            else
            {
                condition.Permanent = true;
                condition.fromEnding = true;
            }
        }

        // 평화·보스·대관식 단계의 250틱 처리: 꽃안개가 (불러오기·다른 효과 등으로) 사라졌으면 다시 건다.
        private void EnsureGoldenBloom()
        {
            var map = AnchorMap;
            if (map != null && GoldenBloomUtility.GetActiveCondition(map)?.fromEnding != true)
            {
                StartGoldenBloom();
            }
        }

        // 엔딩 완료: 꽃안개는 잠시 남았다가 걷힌다(이후로는 의식으로 부른 꽃안개처럼 다룬다).
        private static void FadeGoldenBloom()
        {
            foreach (var map in Find.Maps)
            {
                var condition = GoldenBloomUtility.GetActiveCondition(map);
                if (condition == null || !condition.fromEnding) { continue; }

                condition.fromEnding = false;
                condition.Permanent = false;
                condition.Duration = condition.TicksPassed + GoldenBloomFadeTicks;
            }
        }

        // 엔딩 실패·초기화: 엔딩이 건 꽃안개를 곧바로 걷는다.
        private static void EndGoldenBloom()
        {
            foreach (var map in Find.Maps)
            {
                var condition = GoldenBloomUtility.GetActiveCondition(map);
                if (condition != null && condition.fromEnding)
                {
                    condition.End();
                }
            }
        }
    }
}
