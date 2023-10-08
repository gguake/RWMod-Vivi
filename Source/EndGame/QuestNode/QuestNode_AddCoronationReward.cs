using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace VVRace
{
    public class QuestNode_AddCoronationReward : QuestNode
    {
        [NoTranslate]
        public SlateRef<string> inSignalChoiceUsed;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            var slate = QuestGen.slate;

            var questPart_Choice = new QuestPart_Choice();
            questPart_Choice.inSignalChoiceUsed = QuestGenUtility.HardcodedSignalWithQuestID(inSignalChoiceUsed.GetValue(slate));

            var choice = new QuestPart_Choice.Choice();
            choice.rewards.Add(new Reward_Coronation());

            questPart_Choice.choices.Add(choice);
            QuestGen.quest.AddPart(questPart_Choice);
        }
    }
}
