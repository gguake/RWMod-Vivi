using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 요정 비비 결속의 페널티 스탯(배율). 6단계 영원꽃의 공명자는 def의 각 단계에서 이 스탯의 배율만 뺀 단계를 쓴다.
    public class FairyBondPenaltyExtension : DefModExtension
    {
        public List<StatDef> penaltyStatFactors;
    }

    // 요정 비비 결속(VV_ViviFairyFollow). 6단계 영원꽃의 공명자는 결속 수에 따른 페널티를 받지 않는다.
    // 스탯·툴팁·라벨이 모두 CurStage를 읽으므로 단계만 교체하면 별도의 갱신 처리가 필요 없다.
    // 구 세이브의 예전 클래스(HediffWithComps) 결속은 불러올 때 이 클래스로 바꾼다(BackCompatibilityPatch).
    public class Hediff_ViviFairyFollow : HediffWithComps
    {
        public const int PenaltyFreeAttunementLevel = 6;

        // def별 페널티 없는 단계 목록(def의 stages와 같은 인덱스). 모든 인스턴스가 공유하며 처음 쓸 때 만든다.
        private static readonly Dictionary<HediffDef, List<HediffStage>> PenaltyFreeStagesCache = new Dictionary<HediffDef, List<HediffStage>>();

        // CurStage는 스탯을 계산할 때마다 여러 번 호출되므로 조회 결과를 캐시한다.
        [Unsaved]
        private CompVivi _compVivi;

        public override HediffStage CurStage
        {
            get
            {
                var stage = base.CurStage;
                if (stage == null || !IsPenaltyFree) { return stage; }

                var stagesWithoutPenalty = GetPenaltyFreeStages(def);
                var index = CurStageIndex;
                if (stagesWithoutPenalty != null && index >= 0 && index < stagesWithoutPenalty.Count)
                {
                    return stagesWithoutPenalty[index];
                }

                return stage;
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

        private static List<HediffStage> GetPenaltyFreeStages(HediffDef def)
        {
            if (!PenaltyFreeStagesCache.TryGetValue(def, out var stages))
            {
                stages = MakePenaltyFreeStages(def);
                PenaltyFreeStagesCache.Add(def, stages);
            }

            return stages;
        }

        // 단계를 XML에 따로 복사해 두지 않고 def의 단계에서 페널티 배율만 빼서 만든다.
        // 단계 수치를 고치거나 다른 모드가 패치해도 6단계 공명자의 단계에 그대로 반영된다.
        private static List<HediffStage> MakePenaltyFreeStages(HediffDef def)
        {
            var penaltyStats = def.GetModExtension<FairyBondPenaltyExtension>()?.penaltyStatFactors;
            if (def.stages == null || penaltyStats.NullOrEmpty()) { return null; }

            var stages = new List<HediffStage>(def.stages.Count);
            foreach (var stage in def.stages)
            {
                var copy = Gen.MemberwiseClone(stage);
                copy.statFactors = stage.statFactors?.Where(modifier => !penaltyStats.Contains(modifier.stat)).ToList();
                stages.Add(copy);
            }

            return stages;
        }
    }
}
