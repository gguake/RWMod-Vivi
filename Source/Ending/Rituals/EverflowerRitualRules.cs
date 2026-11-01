using RimWorld;
using Verse;

namespace VVRace
{
    public static class EverflowerRitualRules
    {
        // 〈만개하는 여명〉이 진행되는 동안 앵커 영원꽃에서는 엔딩 의식(대조율·대관식) 외의 의식을 치를 수 없다.
        public static bool IsBlockedByEnding(ArcanePlant_Everflower everflower)
        {
            var quest = ViviEndingQuest.Current;
            return everflower != null && quest != null && quest.IsEndingActive && quest.IsAnchor(everflower);
        }

        // 대조율은 엔딩을 시작하고 대관식은 끝낸다. 각자 대상 필터에서 엔딩 단계를 따로 검사한다.
        public static bool IsEndingRitual(Precept_Ritual ritual)
        {
            return ritual != null && (ritual.def == EndingRitualDefOf.VV_GrandAttunement || ritual.def == EndingRitualDefOf.VV_QueensCoronation);
        }

        public static bool IsCoronationRitual(Precept_Ritual ritual)
        {
            return ritual != null && ritual.def == EndingRitualDefOf.VV_QueensCoronation;
        }

        // 시작 가능하면 null, 불가능하면 기즈모에 표시할 비활성 사유를 반환한다.
        public static string GetBlockReason(ArcanePlant_Everflower everflower, Precept_Ritual ritual)
        {
            if (everflower == null) { return null; }

            if (IsBlockedByEnding(everflower) && !IsEndingRitual(ritual))
            {
                return LocalizeString_EndingRituals.VV_FailReason_EndingInProgress.Translate();
            }

            if (ritual?.def == VVPreceptDefOf.VV_ChangeWeatherRitual && GoldenBloomUtility.IsHeldByEnding(everflower.MapHeld))
            {
                return LocalizeString_EndingCore.VV_FailReason_GoldenBloomHoldsWeather.Translate();
            }

            // 토벌 직후 대관식을 진행할 수 있도록 재사용 대기를 무시한다. 단계 조건은 대상 필터에서 검사한다.
            if (IsCoronationRitual(ritual) && GetCoronationBlockReason(everflower) == null) { return null; }

            if (everflower.HasRitualCooldown)
            {
                var cooldownTicksRemaining = everflower.CurRitualCooldownTicks - GenTicks.TicksGame;
                return LocalizeString_Etc.VV_FailReason_RitualCooldown.Translate(cooldownTicksRemaining.ToStringTicksToPeriod());
            }

            return null;
        }

        // 엔딩이 시작되면 앵커에서 진행 중이던 다른 의식은 취소된다.
        public static bool CanContinueRitual(ArcanePlant_Everflower everflower, Precept_Ritual ritual)
        {
            return !IsBlockedByEnding(everflower) || IsEndingRitual(ritual);
        }

        public static string GetEndingStartBlockReason()
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null) { return null; }

            switch (quest.Stage)
            {
                case ViviEndingStage.None:
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

        // 마나 파동의 습격도 엔딩의 위협 억제를 따른다. 보스 등장이 미뤄져 억제가 풀리면 다시 허용한다.
        public static bool ShouldBlockManawaveRaids(Map map)
        {
            var quest = ViviEndingQuest.Current;
            return quest != null && quest.ShouldSuppressThreats(map);
        }
    }
}
