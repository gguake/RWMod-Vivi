using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    public static class ViviEndingRequirements
    {
        public const int MinViviCount = 10;
        public const float MinColonyWealth = 350000f;

        public static int CountPlayerVivi()
        {
            var count = 0;
            var pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
            for (int i = 0; i < pawns.Count; ++i)
            {
                if (pawns[i].IsVivi())
                {
                    ++count;
                }
            }

            return count;
        }

        public static string ProgressText()
        {
            var sb = new StringBuilder();
            sb.Append(LocalizeString_Etc.VV_Quest_EndingMissiveConditionsHeader.Translate().Resolve());

            var viviCount = CountPlayerVivi();
            AppendLine(sb, viviCount >= MinViviCount, LocalizeString_Etc.VV_Quest_EndingMissiveConditionVivi.Translate(viviCount, MinViviCount).Resolve());

            var wealth = WealthUtility.PlayerWealth;
            AppendLine(sb, wealth >= MinColonyWealth, LocalizeString_Etc.VV_Quest_EndingMissiveConditionWealth.Translate(
                wealth.ToString("N0"), MinColonyWealth.ToString("N0")).Resolve());

            var everflower = ViviEndingUtility.FindEndingCandidate();
            var altar = ViviEndingUtility.AltarOf(everflower);
            AppendLine(sb, altar != null, LocalizeString_Etc.VV_Quest_EndingMissiveStepAltar.Translate().Resolve());
            AppendLine(sb, everflower != null && everflower.EverflowerComp.IsEnshrined && everflower.AttunementLevel >= Building_EverflowerAltar.RequiredAttunementLevel,
                LocalizeString_Etc.VV_Quest_EndingMissiveStepEnshrine.Translate(Building_EverflowerAltar.RequiredAttunementLevel).Resolve());

            return sb.ToString();
        }

        public static string GetUnmetReason()
        {
            var unmet = new List<string>();

            var viviCount = CountPlayerVivi();
            if (viviCount < MinViviCount)
            {
                unmet.Add(LocalizeString_Etc.VV_Quest_EndingMissiveConditionVivi.Translate(viviCount, MinViviCount).Resolve());
            }

            var wealth = WealthUtility.PlayerWealth;
            if (wealth < MinColonyWealth)
            {
                unmet.Add(LocalizeString_Etc.VV_Quest_EndingMissiveConditionWealth.Translate(
                    wealth.ToString("N0"), MinColonyWealth.ToString("N0")).Resolve());
            }

            if (unmet.Count == 0) { return null; }

            return LocalizeString_Dialog.VV_Comms_AmberCoreRequestConditions.Translate(unmet.ToCommaList()).Resolve();
        }

        private static void AppendLine(StringBuilder sb, bool met, string text)
        {
            sb.AppendLine();
            sb.Append("  - ");
            sb.Append(text.Colorize(met ? ColorLibrary.LightGreen : ColorLibrary.RedReadable));
        }
    }
}
