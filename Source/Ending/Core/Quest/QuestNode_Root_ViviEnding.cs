using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace VVRace
{
    // 동화책 1권부터 대관식까지 하나로 이어지는 에픽 퀘스트. 개화 이후 부품은 만개 의식 때 ViviEndingBloomParts가 덧붙인다.
    // ViviEndingQuest.GenerateQuest는 EndingFinished 성공 QuestEnd의 존재로 생성 완료를 확인한다.
    public class QuestNode_Root_ViviEnding : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            var quest = QuestGen.quest;
            var slate = QuestGen.slate;

            slate.Set("resolvedQuestName", LocalizeString_Etc.VV_Quest_EndingName.Translate().Resolve());
            slate.Set("resolvedQuestDescription", LocalizeString_Etc.VV_Quest_EndingDesc.Translate().Resolve());

            quest.AddPart(new QuestPart_ViviEndingJourney());

            ViviEndingBloomParts.AddEnd(quest, QuestEndOutcome.Success, QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.EndingFinished));
        }
    }
}
