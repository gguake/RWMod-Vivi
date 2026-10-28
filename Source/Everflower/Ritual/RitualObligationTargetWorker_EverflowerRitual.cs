using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualObligationTargetFilterDef_EverflowerRitual : RitualObligationTargetFilterDef
    {
        public int requiredAttunementLevel;

        // 이 단계를 넘는 영원꽃에서는 의식을 숨긴다(대조율 의식은 4단계에서만 보인다).
        public int maxAttunementLevel = 99;

        // 영원꽃 제단에 안치된 영원꽃에서만 진행할 수 있는지.
        public bool requiresAltar;

        // 아포크리파 2페이즈 동안 앵커 영원꽃에서도 진행할 수 있는지(조율 의식만 true).
        public bool allowedDuringApocryphaPhase2;

        // 엔딩 퀘스트를 시작하는 의식인지(대조율 의식). 비비 연방의 서신을 받은 뒤, 엔딩이 시작되기 전에만 진행할 수 있다.
        public bool startsEndingQuest;

        // 엔딩 퀘스트를 끝내는 의식인지(여왕의 대관식). 아포크리파를 토벌한 뒤 앵커 영원꽃에서 한 번만 진행할 수 있다.
        public bool completesEndingQuest;
    }

    public class RitualObligationTargetWorker_EverflowerRitual : RitualObligationTargetFilter
    {
        public RitualObligationTargetFilterDef_EverflowerRitual Def => (RitualObligationTargetFilterDef_EverflowerRitual)def;

        public RitualObligationTargetWorker_EverflowerRitual() { }
        public RitualObligationTargetWorker_EverflowerRitual(RitualObligationTargetFilterDef def) : base(def) { }

        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            yield return LocalizeString_Etc.VV_RitualTarget_Everflower.Translate();
            yield break;
        }

        public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
        {
            foreach (var everflower in map.listerThings.ThingsOfDef(VVThingDefOf.VV_Everflower))
            {
                if (everflower.TryGetComp<CompEverflower>(out var compEverflower) &&
                    compEverflower.AttunementLevel >= Def.requiredAttunementLevel &&
                    compEverflower.AttunementLevel <= Def.maxAttunementLevel)
                {
                    yield return everflower;
                }
            }
        }

        // false를 반환하면 기즈모가 숨겨지고, 사유 문자열을 반환하면 비활성 상태로 사유와 함께 표시된다.
        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
            if (!target.HasThing) { return false; }

            if (!target.Thing.TryGetComp<CompEverflower>(out var compEverflower))
            {
                return false;
            }

            if (compEverflower.AttunementLevel < Def.requiredAttunementLevel || compEverflower.AttunementLevel > Def.maxAttunementLevel)
            {
                return false;
            }

            if (!Def.allowedDuringApocryphaPhase2 && EverflowerRitualRules.IsResonancePhase(compEverflower.Everflower))
            {
                return LocalizeString_EndingRituals.VV_FailReason_ApocryphaInterference.Translate();
            }

            // 엔딩 진행 단계에 따른 불가 사유를 제단 관련 사유보다 먼저 확인한다. 서신 전에는 제단을 연구·건설할 수 없으므로 "제단에 안치하라"보다 "서신 뒤에 열린다"가 맞다.
            if (Def.startsEndingQuest)
            {
                var reason = EverflowerRitualRules.GetEndingStartBlockReason();
                if (reason != null)
                {
                    return reason;
                }
            }

            if (Def.completesEndingQuest)
            {
                var reason = EverflowerRitualRules.GetCoronationBlockReason(compEverflower.Everflower);
                if (reason != null)
                {
                    return reason;
                }
            }

            if (Def.requiresAltar && !compEverflower.IsEnshrined)
            {
                return LocalizeString_EndingRituals.VV_FailReason_RequiresEverflowerAltar.Translate();
            }

            return true;
        }
    }
}
