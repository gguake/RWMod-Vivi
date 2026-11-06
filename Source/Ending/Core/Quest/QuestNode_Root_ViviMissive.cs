using RimWorld;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // ViviEndingQuest.GenerateQuest는 마지막 성공 QuestEnd의 존재로 생성 완료를 확인한다.
    public class QuestNode_Root_ViviMissive : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            var quest = QuestGen.quest;
            var slate = QuestGen.slate;

            var everflower = slate.Get<ArcanePlant_Everflower>("everflower");
            var resonator = ViviEndingUtility.ResonatorOf(everflower);
            var missiveLabel = LocalizeString_Letter.VV_Letter_EndingFederationMissiveLabel.Translate();
            var missiveText = ViviEndingUtility.TranslateWithResonator(LocalizeString_Letter.VV_Letter_EndingFederationMissive, resonator).Resolve()
                + "\n\n" + LocalizeString_Letter.VV_Letter_EndingFederationMissiveConditions.Translate().Resolve();

            slate.Set("resolvedQuestName", missiveLabel.Resolve());
            slate.Set("resolvedQuestDescription", missiveText + "\n\n" + LocalizeString_Etc.VV_Quest_EndingMissiveGoals.Translate().Resolve());

            var hyperlinks = new List<ThingDef>();
            if (VVThingDefOf.VV_EverflowerAltar != null)
            {
                hyperlinks.Add(VVThingDefOf.VV_EverflowerAltar);
            }
            if (VVThingDefOf.VV_AncientAmberCore != null)
            {
                hyperlinks.Add(VVThingDefOf.VV_AncientAmberCore);
            }

            if (!slate.Get<bool>("reissued"))
            {
                var lookTargets = everflower != null && everflower.SpawnedOrAnyParentSpawned ? new LookTargets(everflower) : LookTargets.Invalid;
                quest.AddPart(new QuestPart_Letter()
                {
                    inSignal = quest.InitiateSignal,
                    letter = LetterMaker.MakeLetter(missiveLabel, missiveText, LetterDefOf.PositiveEvent, lookTargets, ViviEndingUtility.FindFederation(requireNonHostile: false), quest, hyperlinks),
                });
            }

            quest.AddPart(new QuestPart_ViviMissiveRequirements());

            quest.End(QuestEndOutcome.Success, inSignal: QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.GrandAttunementCompleted));
        }
    }
}
