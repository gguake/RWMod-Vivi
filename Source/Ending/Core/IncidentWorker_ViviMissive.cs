using RimWorld;
using Verse;

namespace VVRace
{
    // 〈비비 연방의 밀서〉 사건(Incidents_Ending.xml). 스토리텔러의 무작위 퀘스트 주기가 고르며,
    // 플레이어 영원꽃 4단계·기지 가치 350,000·플레이어 소속 비비 10명 이상·패망하지 않은 비적대 비비 연방을 모두 갖춰야 발생한다.
    // 퀘스트는 바닐라 GiveQuest 대신 창구(ViviEndingQuest)가 만들어 참조를 보관한다.
    public class IncidentWorker_ViviMissive : IncidentWorker_GiveQuest
    {
        public const int MinAttunementLevel = 4;
        public const float MinColonyWealth = 350000f;
        public const int MinViviCount = 10;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms) && ConditionsMet();
        }

        // 밀서를 만들지 못하면(생성 오류, 이미 기록됨) 사건도 일어나지 않은 것으로 쳐서 다음 기회에 다시 고를 수 있게 한다.
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            return ViviEndingQuest.Current?.StartMissiveQuest(null) == true;
        }

        // 밀서가 아직 없고 발생 조건을 모두 갖췄는지.
        public static bool ConditionsMet()
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null || quest.Stage != ViviEndingStage.None) { return false; }

            var everflower = ViviEndingUtility.FindEndingCandidate();
            if (everflower == null || everflower.AttunementLevel < MinAttunementLevel) { return false; }

            if (WealthUtility.PlayerWealth < MinColonyWealth) { return false; }
            if (CountPlayerVivi() < MinViviCount) { return false; }

            return ViviEndingUtility.FindFederation(requireNonHostile: true) != null;
        }

        // 살아 있는 플레이어 소속 비비(정착민·노예, 나이 무관). 맵·캐러밴·이동 중인 수송 포드에 있는 폰을 센다.
        private static int CountPlayerVivi()
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
    }
}
