using RimWorld;
using Verse;

namespace VVRace
{
    // 영원꽃 의식의 시작/지속 가능 여부를 한 곳에서 판단한다.
    // 세 행동 워커(조율/일반/요정화)의 CanStartRitualNow와 대상 필터가 이 규칙을 공유한다.
    public static class EverflowerRitualRules
    {
        // 앵커 영원꽃이 아포크리파 2페이즈 전투 중인지. 이때는 조율 의식(공명 파동)만 재사용 대기 없이 허용된다.
        public static bool IsResonancePhase(ArcanePlant_Everflower everflower)
        {
            if (everflower == null) { return false; }

            var quest = ViviEndingQuest.Current;
            return quest != null && quest.IsBossPhase2 && quest.IsAnchor(everflower);
        }

        public static bool IsResonanceRitual(Precept_Ritual ritual)
        {
            return ritual != null && ritual.def == EndingRitualDefOf.VV_Attunement;
        }

        public static bool IsCoronationRitual(Precept_Ritual ritual)
        {
            return ritual != null && ritual.def == EndingRitualDefOf.VV_QueensCoronation;
        }

        // 엔딩이 묶인 앵커 영원꽃인지(단계 무관). 앵커의 의식은 LordJob_RitualResonance로 진행한다.
        public static bool IsAnchor(ArcanePlant_Everflower everflower)
        {
            var quest = ViviEndingQuest.Current;
            return everflower != null && quest != null && quest.IsAnchor(everflower);
        }

        // 보스전(1·2페이즈) 중인지. 이 동안 앵커 영원꽃의 의식은 적이 다가와도 취소되지 않고, 관람자를 모집하지 않으며,
        // 다른 폰(채집자 등)이 영원꽃을 예약해도 취소되지 않는다.
        public static bool IsBossFightActive
        {
            get
            {
                var quest = ViviEndingQuest.Current;
                return quest != null && quest.Stage == ViviEndingStage.Boss;
            }
        }

        public static bool IsBossFightOnMap(Map map)
        {
            var quest = ViviEndingQuest.Current;
            return map != null && quest != null && quest.Stage == ViviEndingStage.Boss && quest.AnchorMap == map;
        }

        public static bool IsAnchorUnderBossFight(ArcanePlant_Everflower everflower)
        {
            return IsBossFightActive && IsAnchor(everflower);
        }

        // 의식을 시작할 수 있으면 null을, 시작할 수 없으면 기즈모의 비활성 사유를 문자열로 반환한다.
        public static string GetBlockReason(ArcanePlant_Everflower everflower, Precept_Ritual ritual)
        {
            if (everflower == null) { return null; }

            if (IsResonancePhase(everflower))
            {
                // 2페이즈: 조율 의식은 재사용 대기를 무시하고, 나머지 의식은 모두 막는다.
                if (IsResonanceRitual(ritual)) { return null; }

                return LocalizeString_EndingRituals.VV_FailReason_ApocryphaInterference.Translate();
            }

            // 엔딩의 마지막 의식(여왕의 대관식)은 아포크리파 토벌 직후 바로 치를 수 있도록 재사용 대기를 무시한다(단계 조건은 대상 필터가 확인한다).
            if (IsCoronationRitual(ritual) && GetCoronationBlockReason(everflower) == null) { return null; }

            // 평화 기간·1페이즈를 포함한 그 외의 경우는 기존 공유 재사용 대기만 따른다.
            if (everflower.HasRitualCooldown)
            {
                var cooldownTicksRemaining = everflower.CurRitualCooldownTicks - GenTicks.TicksGame;
                return LocalizeString_Etc.VV_FailReason_RitualCooldown.Translate(cooldownTicksRemaining.ToStringTicksToPeriod());
            }

            return null;
        }

        // 진행 중인 의식이 계속될 수 있는지. 2페이즈가 시작되면 앵커 영원꽃의 조율 외 의식은 자연스럽게 취소된다.
        public static bool CanContinueRitual(ArcanePlant_Everflower everflower, Precept_Ritual ritual)
        {
            return !IsResonancePhase(everflower) || IsResonanceRitual(ritual);
        }

        // 엔딩을 시작하는 의식(5단계 대조율)은 비비 연방의 서신을 받은 뒤 엔딩이 시작되기 전에만 가능하다.
        public static string GetEndingStartBlockReason()
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null) { return null; }

            switch (quest.Stage)
            {
                case ViviEndingStage.None:
                case ViviEndingStage.LetterPending:
                    return LocalizeString_EndingRituals.VV_FailReason_GrandAttunementLocked.Translate();

                case ViviEndingStage.AltarUnlocked:
                    return null;

                case ViviEndingStage.Completed:
                    return LocalizeString_EndingRituals.VV_FailReason_GrandAttunementCompleted.Translate();

                case ViviEndingStage.Failed:
                    return LocalizeString_EndingRituals.VV_FailReason_GrandAttunementFailed.Translate();

                default:
                    return LocalizeString_EndingRituals.VV_FailReason_GrandAttunementInProgress.Translate();
            }
        }

        // 엔딩을 끝내는 의식(여왕의 대관식)은 아포크리파를 토벌해 응축이 100%가 된 앵커 영원꽃에서 한 번만 가능하다.
        public static string GetCoronationBlockReason(ArcanePlant_Everflower everflower)
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null || !quest.IsAnchor(everflower))
            {
                return LocalizeString_EndingRituals.VV_FailReason_CoronationNotAnchor.Translate();
            }

            if (!quest.IsAwaitingCoronation)
            {
                return LocalizeString_EndingRituals.VV_FailReason_CoronationLocked.Translate();
            }

            return null;
        }

        // 엔딩의 평화 기간/보스전 동안에는 마나 파동 의식이 새 습격을 불러들이지 않는다(기절·마나 효과는 그대로).
        public static bool ShouldBlockManawaveRaids(Map map)
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null || map == null || quest.AnchorMap != map) { return false; }

            return quest.Stage == ViviEndingStage.Calm || quest.Stage == ViviEndingStage.Boss;
        }
    }
}
