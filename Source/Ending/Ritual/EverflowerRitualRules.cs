using RimWorld;
using Verse;

namespace VVRace
{
    public static class EverflowerRitualRules
    {
        public static bool IsBlockedByEnding(ArcanePlant_Everflower everflower)
        {
            var quest = ViviEndingQuest.Current;
            return everflower != null && quest != null && quest.IsEndingActive && quest.IsAnchor(everflower);
        }

        public static bool IsEndingRitual(Precept_Ritual ritual)
        {
            return ritual != null && (ritual.def == VVPreceptDefOf.VV_GrandAttunement || ritual.def == VVPreceptDefOf.VV_QueensCoronation);
        }

        public static bool IsAttunementAllowedDuringEnding(Precept_Ritual ritual)
        {
            return ritual != null && ritual.def == VVPreceptDefOf.VV_Attunement
                && (ViviEndingQuest.Current?.IsAwaitingCoronation ?? false);
        }

        private static bool IsAllowedDuringEnding(Precept_Ritual ritual)
        {
            return IsEndingRitual(ritual) || IsAttunementAllowedDuringEnding(ritual);
        }

        public static string GetBlockReason(ArcanePlant_Everflower everflower, Precept_Ritual ritual)
        {
            if (everflower == null) { return null; }

            if (IsBlockedByEnding(everflower) && !IsAllowedDuringEnding(ritual))
            {
                return LocalizeString_Etc.VV_FailReason_EndingInProgress.Translate();
            }

            if (ritual?.def == VVPreceptDefOf.VV_ChangeWeatherRitual && GoldenBloomUtility.IsHeldByEnding(everflower.MapHeld))
            {
                return LocalizeString_Etc.VV_FailReason_GoldenBloomHoldsWeather.Translate();
            }

            if (ritual != null && ritual.def == VVPreceptDefOf.VV_QueensCoronation
                && GetCoronationBlockReason(everflower) == null) { return null; }

            if (everflower.HasRitualCooldown)
            {
                var cooldownTicksRemaining = everflower.CurRitualCooldownTicks - GenTicks.TicksGame;
                return LocalizeString_Etc.VV_FailReason_RitualCooldown.Translate(cooldownTicksRemaining.ToStringTicksToPeriod());
            }

            return null;
        }

        public static bool CanContinueRitual(ArcanePlant_Everflower everflower, Precept_Ritual ritual)
        {
            return !IsBlockedByEnding(everflower) || IsAllowedDuringEnding(ritual);
        }

        public static string GetEndingStartBlockReason()
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null) { return null; }

            switch (quest.Stage)
            {
                case ViviEndingStage.None:
                    return LocalizeString_Etc.VV_FailReason_GrandAttunementLocked.Translate();

                case ViviEndingStage.AltarUnlocked:
                    return null;

                case ViviEndingStage.Completed:
                    return LocalizeString_Etc.VV_FailReason_GrandAttunementCompleted.Translate();

                case ViviEndingStage.Failed:
                    return LocalizeString_Etc.VV_FailReason_GrandAttunementFailed.Translate();

                default:
                    return LocalizeString_Etc.VV_FailReason_GrandAttunementInProgress.Translate();
            }
        }

        public static string GetCoronationBlockReason(ArcanePlant_Everflower everflower)
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null || !quest.IsAnchor(everflower))
            {
                return LocalizeString_Etc.VV_FailReason_CoronationNotAnchor.Translate();
            }

            if (!quest.IsAwaitingCoronation)
            {
                return LocalizeString_Etc.VV_FailReason_CoronationLocked.Translate();
            }

            return null;
        }
    }
}
