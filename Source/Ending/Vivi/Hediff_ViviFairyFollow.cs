using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 요정 비비 결속 단계 중 피로·이동속도 페널티를 뺀 단계 목록. 순서와 개수는 def의 stages와 같아야 한다.
    public class FairyBondStagesExtension : DefModExtension
    {
        public List<HediffStage> stagesWithoutPenalty;
    }

    // 요정 비비 결속(VV_ViviFairyFollow). 6단계 영원꽃의 공명자는 결속 수에 따른 페널티를 받지 않는다.
    // 스탯·툴팁·라벨이 모두 CurStage를 읽으므로 단계만 교체하면 별도의 갱신 처리가 필요 없다.
    public class Hediff_ViviFairyFollow : HediffWithComps
    {
        public const int PenaltyFreeAttunementLevel = 6;

        // CurStage는 스탯을 계산할 때마다 여러 번 호출되므로 조회 결과를 캐시한다.
        [Unsaved]
        private FairyBondStagesExtension _stagesExtension;
        [Unsaved]
        private bool _stagesExtensionCached;
        [Unsaved]
        private CompVivi _compVivi;

        public override HediffStage CurStage
        {
            get
            {
                var stage = base.CurStage;
                if (stage == null || !IsPenaltyFree) { return stage; }

                var stagesWithoutPenalty = StagesExtension?.stagesWithoutPenalty;
                var index = CurStageIndex;
                if (stagesWithoutPenalty != null && index >= 0 && index < stagesWithoutPenalty.Count)
                {
                    return stagesWithoutPenalty[index];
                }

                return stage;
            }
        }

        private FairyBondStagesExtension StagesExtension
        {
            get
            {
                if (!_stagesExtensionCached)
                {
                    _stagesExtension = def.GetModExtension<FairyBondStagesExtension>();
                    _stagesExtensionCached = true;
                }

                return _stagesExtension;
            }
        }

        private bool IsPenaltyFree
        {
            get
            {
                if (_compVivi == null)
                {
                    _compVivi = pawn?.GetCompVivi();
                }

                var linkedEverflower = _compVivi?.LinkedEverflower;
                return linkedEverflower != null && linkedEverflower.AttunementLevel >= PenaltyFreeAttunementLevel;
            }
        }
    }
}
