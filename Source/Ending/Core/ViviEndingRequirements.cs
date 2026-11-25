using RimWorld;
using System.Text;
using Verse;

namespace VVRace
{
    public static class ViviEndingRequirements
    {
        public static int CountPlayerVivi(Pawn exclude = null)
        {
            var count = 0;
            var pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
            for (int i = 0; i < pawns.Count; ++i)
            {
                if (pawns[i] != exclude && pawns[i].IsVivi())
                {
                    ++count;
                }
            }

            return count;
        }

        public static string AltarStepsText()
        {
            var sb = new StringBuilder();
            var everflower = ViviEndingUtility.FindEndingCandidate();
            var altar = ViviEndingUtility.AltarOf(everflower);

            AppendLine(sb, altar != null, LocalizeString_Etc.VV_Quest_EndingStepAltar.Translate().Resolve());
            AppendLine(sb, everflower != null && everflower.EverflowerComp.IsEnshrined && everflower.AttunementLevel >= Building_EverflowerAltar.RequiredAttunementLevel,
                LocalizeString_Etc.VV_Quest_EndingStepEnshrine.Translate(Building_EverflowerAltar.RequiredAttunementLevel).Resolve());

            return sb.ToString();
        }

        private static void AppendLine(StringBuilder sb, bool met, string text)
        {
            sb.AppendLine();
            sb.Append("  - ");
            sb.Append(text.Colorize(met ? ColorLibrary.LightGreen : ColorLibrary.RedReadable));
        }
    }
}
