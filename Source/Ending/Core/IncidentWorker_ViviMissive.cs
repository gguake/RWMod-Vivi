using RimWorld;
using Verse;

namespace VVRace
{
    // 〈비비 연방의 밀서〉 사건(Incidents_Ending.xml). 밀서는 도착 조건(1~4단계 잔향 편지 확인·정착지의 조율 4단계 영원꽃·비적대 비비 연방)을 갖추면
    // 창구(ViviEndingQuest.TickMissiveArrival)가 1~2일 뒤 확정으로 보내므로, 스토리텔러는 이 사건을 고르지 않는다(baseChance 0).
    // 개발자 도구의 사건 실행으로 같은 조건에서 바로 보낼 수 있게 남겨 둔다.
    // 퀘스트는 바닐라 GiveQuest 대신 창구(ViviEndingQuest)가 만들어 참조를 보관한다.
    public class IncidentWorker_ViviMissive : IncidentWorker_GiveQuest
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms) && ViviEndingQuest.MissiveArrivalConditionsMet();
        }

        // 밀서를 만들지 못하면(생성 오류, 이미 기록됨) 사건도 일어나지 않은 것으로 친다.
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            return ViviEndingQuest.Current?.StartMissiveQuest(null) == true;
        }
    }
}
