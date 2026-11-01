using RimWorld;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 슬레이트 입력: everflower(서신의 공명자·편지 대상, 없을 수 있음), reissued(재생성 시 서신 재발송 생략).
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
            var missiveLabel = LocalizeString_EndingCore.VV_Letter_EndingFederationMissiveLabel.Translate();
            var missiveText = ViviEndingUtility.TranslateWithResonator(LocalizeString_EndingCore.VV_Letter_EndingFederationMissive, resonator).Resolve()
                + "\n\n" + ViviEndingRequirements.LetterText();

            slate.Set("resolvedQuestName", missiveLabel.Resolve());
            slate.Set("resolvedQuestDescription", missiveText + "\n\n" + LocalizeString_EndingCore.VV_Quest_EndingMissiveGoals.Translate().Resolve());

            var hyperlinks = new List<ThingDef>();
            if (EndingCoreDefOf.VV_DreamumAltar != null)
            {
                hyperlinks.Add(EndingCoreDefOf.VV_DreamumAltar);
            }
            if (EndingCoreDefOf.VV_AncientAmberCore != null)
            {
                hyperlinks.Add(EndingCoreDefOf.VV_AncientAmberCore);
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
