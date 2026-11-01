using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    // 〈비비 연방의 밀서〉에 적힌 엔딩 진행 조건. 밀서 자체는 조율 4단계와 1~4단계 잔향 편지만 갖추면 잠시 뒤 확정으로 도착하고(ViviEndingQuest.TickMissiveArrival),
    // 플레이어 소속 비비 수·기지 가치는 밀서 편지와 퀘스트 탭에 진행 조건으로 보여 주고, 통신기로 고대 호박 코어 일거리를 의뢰할 때 확인한다
    // (코어가 있어야 제단을 지을 수 있으므로 〈대조율 의식〉도 자연히 이 조건 뒤에 열린다).
    public static class ViviEndingRequirements
    {
        public const int MinViviCount = 10;
        public const float MinColonyWealth = 350000f;

        public static bool ViviCountMet => CountPlayerVivi() >= MinViviCount;
        public static bool WealthMet => WealthUtility.PlayerWealth >= MinColonyWealth;
        public static bool AllMet => ViviCountMet && WealthMet;

        // 살아 있는 플레이어 소속 비비(정착민·노예, 나이 무관). 맵·캐러밴·이동 중인 수송 포드에 있는 폰을 센다.
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

        // 밀서 편지 끝에 붙이는 진행 조건 안내(목표치만).
        public static string LetterText()
        {
            return LocalizeString_EndingCore.VV_Letter_EndingFederationMissiveConditions.Translate(
                MinViviCount.Named("VIVI"),
                MinColonyWealth.ToString("N0").Named("WEALTH")).Resolve();
        }

        // 퀘스트 탭에 붙는 진행 상황: 진행 조건(비비 수·기지 가치)과 준비 단계(제단 건설·영원꽃 안치)를 현재 값과 함께 갖춘 것은 초록, 못 갖춘 것은 빨강으로.
        public static string ProgressText()
        {
            var sb = new StringBuilder();
            sb.Append(LocalizeString_EndingCore.VV_Quest_EndingMissiveConditionsHeader.Translate().Resolve());

            var viviCount = CountPlayerVivi();
            AppendLine(sb, viviCount >= MinViviCount, LocalizeString_EndingCore.VV_Quest_EndingMissiveConditionVivi.Translate(viviCount, MinViviCount).Resolve());

            var wealth = WealthUtility.PlayerWealth;
            AppendLine(sb, wealth >= MinColonyWealth, LocalizeString_EndingCore.VV_Quest_EndingMissiveConditionWealth.Translate(
                wealth.ToString("N0"), MinColonyWealth.ToString("N0")).Resolve());

            var everflower = ViviEndingUtility.FindEndingCandidate();
            var altar = ViviEndingUtility.AltarOf(everflower);
            AppendLine(sb, altar != null, LocalizeString_EndingCore.VV_Quest_EndingMissiveStepAltar.Translate().Resolve());
            AppendLine(sb, everflower != null && everflower.EverflowerComp.IsEnshrined && everflower.AttunementLevel >= Building_DreamumAltar.RequiredAttunementLevel,
                LocalizeString_EndingCore.VV_Quest_EndingMissiveStepEnshrine.Translate(Building_DreamumAltar.RequiredAttunementLevel).Resolve());

            return sb.ToString();
        }

        // 통신기 고대 호박 코어 의뢰 선택지의 비활성 사유(조건을 모두 갖췄으면 null).
        public static string GetUnmetReason()
        {
            var unmet = new List<string>();

            var viviCount = CountPlayerVivi();
            if (viviCount < MinViviCount)
            {
                unmet.Add(LocalizeString_EndingCore.VV_Quest_EndingMissiveConditionVivi.Translate(viviCount, MinViviCount).Resolve());
            }

            var wealth = WealthUtility.PlayerWealth;
            if (wealth < MinColonyWealth)
            {
                unmet.Add(LocalizeString_EndingCore.VV_Quest_EndingMissiveConditionWealth.Translate(
                    wealth.ToString("N0"), MinColonyWealth.ToString("N0")).Resolve());
            }

            if (unmet.Count == 0) { return null; }

            return LocalizeString_EndingCore.VV_Comms_AmberCoreRequestConditions.Translate(unmet.ToCommaList()).Resolve();
        }

        private static void AppendLine(StringBuilder sb, bool met, string text)
        {
            sb.AppendLine();
            sb.Append("  - ");
            sb.Append(text.Colorize(met ? ColorLibrary.LightGreen : ColorLibrary.RedReadable));
        }
    }
}
