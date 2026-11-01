using RimWorld;
using Verse;

namespace VVRace
{
    // 〈영원꽃의 잔향〉: 영원꽃 조율 1~5단계에 대응하는 서사 편지. 한 게임에서 단계마다 한 번씩, 1 → 5 순서로만 나온다.
    // 일반 영원꽃 의식(조율·날씨 변경·요정화·요정화 해제·마나 파동)과 5단계에 진입한 〈대조율 의식〉이 끝날 때 의식의 공명자(로열 비비)를 주인공으로 보낸다.
    // 2페이즈 공명 파동·〈여왕의 대관식〉·〈생명의 회귀〉와 효과 없이 끝난 의식은 부르지 않는다.
    // 〈비비 연방의 밀서〉는 1~4단계 편지를 모두 본 뒤에만 도착하므로(ViviEndingQuest.MissiveArrivalConditionsMet) 정상 진행이면 대조율 때 곧바로 5단계 편지가 나온다.
    public partial class ViviEndingQuest
    {
        public const int MaxEchoStage = 5;

        // 다음 편지가 지금 조율 단계의 편지일 때(밀린 편지가 아닐 때) 일반 의식 한 번에 나올 확률.
        private const float CurrentLevelEchoChance = 0.5f;

        private static readonly string[] EchoLetterLabelKeys =
        {
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho1Label,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho2Label,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho3Label,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho4Label,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho5Label,
        };

        private static readonly string[] EchoLetterTextKeys =
        {
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho1,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho2,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho3,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho4,
            LocalizeString_EndingCore.VV_Letter_EverflowerEcho5,
        };

        // 마지막으로 보낸 잔향 편지 단계(0: 아직 없음).
        private int _lastEchoStage;

        public int LastEchoStage => _lastEchoStage;

        // [의식 호출] 영원꽃 의식이 끝났을 때. 영원꽃의 조율 단계(L)는 의식 효과(조율 경험치, 대조율 개화)가 반영된 뒤의 값이며, 의식 한 번에 많아야 한 통을 보낸다.
        //   다음 단계 N < L: 이전 단계 편지를 못 보고 단계가 올랐으므로(기존 고단계 세이브 포함) 밀린 편지를 확정으로 보낸다.
        //   N == L, 1~4단계: 확률로 보낸다.
        //   N == L, 5단계: 확정으로 보낸다. 정상 진행이면 대조율 의식이 이 경우이고, 대조율 때 밀린 편지를 보내느라 남았다면 다음 의식에서 보낸다.
        //   N > L: 지금 단계의 편지를 이미 봤으므로 보내지 않는다.
        public void Notify_EverflowerRitualEcho(ArcanePlant_Everflower everflower, Pawn resonator)
        {
            if (everflower == null || resonator == null) { return; }

            var next = _lastEchoStage + 1;
            if (next > MaxEchoStage) { return; }

            var level = everflower.AttunementLevel;
            if (next > level) { return; }
            if (next == level && next < MaxEchoStage && !Rand.Chance(CurrentLevelEchoChance)) { return; }

            SendEchoLetter(next, everflower, resonator);
        }

        private void SendEchoLetter(int stage, ArcanePlant_Everflower everflower, Pawn resonator)
        {
            _lastEchoStage = stage;

            Find.LetterStack.ReceiveLetter(
                EchoLetterLabelKeys[stage - 1].Translate(resonator.Named("PAWN")),
                EchoLetterTextKeys[stage - 1].Translate(resonator.Named("PAWN")),
                LetterDefOf.NeutralEvent,
                new LookTargets(new TargetInfo[] { resonator, everflower }));
        }
    }
}
