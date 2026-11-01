using RimWorld;

namespace VVRace
{
    // 〈비비 연방의 밀서〉 퀘스트 탭 설명 아래에 엔딩 진행 조건(비비 수·기지 가치)과 준비 단계(제단 건설·영원꽃 안치)의 현재 상황을 붙인다.
    // 밀서가 진행 중일 때만 보이며, 이 부품이 생기기 전에 만든 밀서에는 창구(ViviEndingQuest)가 불러온 뒤 덧붙인다.
    public class QuestPart_ViviMissiveRequirements : QuestPart
    {
        public override string DescriptionPart => quest.State == QuestState.Ongoing ? ViviEndingRequirements.ProgressText() : null;
    }
}
