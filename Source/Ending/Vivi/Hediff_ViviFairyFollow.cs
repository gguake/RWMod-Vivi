using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class FairyBondPenaltyExtension : DefModExtension
    {
        public List<StatDef> penaltyStatFactors;
    }

    // 구버전 HediffWithComps는 불러올 때 BackCompatibilityPatch가 이 클래스로 변환한다.
    public class Hediff_ViviFairyFollow : HediffWithComps
    {
        public const int PenaltyFreeAttunementLevel = 6;

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

        // XML에 단계를 복제하지 않고 def의 단계에서 페널티 배율만 제거한다.
        // 원본 수치 변경이나 다른 모드의 패치가 6단계 공명자에게도 반영되도록 한다.
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
