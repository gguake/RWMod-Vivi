using RimWorld;
using Verse;

namespace VVRace
{
    public static class EverflowerRitualRules
    {
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

        // ignoreBleeding을 상시 켜면 일반 조율에도 적용되므로 배정과 참석 유지 판정 중에만 임시로 켠다.
        // 배정은 EndingRitualPatch, 참석 유지는 LordJob_RitualResonance.VoluntaryJoinPriorityFor에서 처리한다.
        public static bool ShouldIgnoreBleeding(RitualRole role, Precept_Ritual ritual, ArcanePlant_Everflower everflower)
        {
            return role != null && !role.ignoreBleeding && IsResonanceRitual(ritual) && IsResonancePhase(everflower);
        }

        public static bool IsAnchor(ArcanePlant_Everflower everflower)
        {
            var quest = ViviEndingQuest.Current;
            return everflower != null && quest != null && quest.IsAnchor(everflower);
        }

        public static bool IsBossFightOnMap(Map map)
        {
            var quest = ViviEndingQuest.Current;
            return map != null && quest != null && quest.Stage == ViviEndingStage.Boss && quest.AnchorMap == map;
        }

        public static bool IsAnchorUnderBossFight(ArcanePlant_Everflower everflower)
        {
            var quest = ViviEndingQuest.Current;
            return everflower != null && quest != null && quest.Stage == ViviEndingStage.Boss && quest.IsAnchor(everflower);
        }

        // 시작 가능하면 null, 불가능하면 기즈모에 표시할 비활성 사유를 반환한다.
        public static string GetBlockReason(ArcanePlant_Everflower everflower, Precept_Ritual ritual)
        {
            if (everflower == null) { return null; }

            if (IsResonancePhase(everflower))
            {
                if (IsResonanceRitual(ritual))
                {
                    var resonanceTicksLeft = ViviEndingQuest.Current?.BossComp?.ResonanceCooldownTicksLeft ?? 0;
                    return resonanceTicksLeft > 0 ? (string)LocalizeString_Etc.VV_FailReason_RitualCooldown.Translate(resonanceTicksLeft.ToStringTicksToPeriod()) : null;
                }

                return LocalizeString_EndingRituals.VV_FailReason_ApocryphaInterference.Translate();
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

        public static bool CanContinueRitual(ArcanePlant_Everflower everflower, Precept_Ritual ritual)
        {
            return !IsResonancePhase(everflower) || IsResonanceRitual(ritual);
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
