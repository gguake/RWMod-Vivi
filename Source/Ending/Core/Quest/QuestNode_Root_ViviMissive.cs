using RimWorld;
using RimWorld.QuestGen;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 〈비비 연방의 밀서〉 퀘스트 생성. 플레이어 영원꽃이 4단계에 이른 뒤 서신이 도착하는 순간 창구(ViviEndingQuest)가 만든다.
    // 슬레이트: everflower(서신 본문의 공명자·편지 대상, 없을 수 있음),
    // reissued·coreDelivered(앞선 밀서 퀘스트가 뜻밖에 끝나 창구가 다시 만들 때: 서신 편지를 다시 보내지 않고, 이미 보낸 코어는 다시 보내지 않는다).
    // 서신 편지와 고대 호박 코어 전달을 맡고, 5단계 대조율 의식이 끝나면(창구가 보내는 신호) 성공으로 끝난다.
    // 마지막 부품(성공 QuestEnd)까지 추가되어야 창구가 온전한 퀘스트로 본다(ViviEndingQuest.GenerateQuest).
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
            var missiveText = ViviEndingUtility.TranslateWithResonator(LocalizeString_EndingCore.VV_Letter_EndingFederationMissive, resonator).Resolve();

            // 이름·설명은 Keyed 번역으로 정한다(문법 규칙을 거치지 않음).
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

            // 서신: 퀘스트가 시작되자마자(자동 수락) 도착한다. 다시 만든 퀘스트는 서신을 다시 보내지 않는다.
            if (!slate.Get<bool>("reissued"))
            {
                var lookTargets = everflower != null && everflower.SpawnedOrAnyParentSpawned ? new LookTargets(everflower) : LookTargets.Invalid;
                quest.AddPart(new QuestPart_Letter()
                {
                    inSignal = quest.InitiateSignal,
                    letter = LetterMaker.MakeLetter(missiveLabel, missiveText, LetterDefOf.PositiveEvent, lookTargets, ViviEndingUtility.FindFederation(requireNonHostile: false), quest, hyperlinks),
                });
            }

            // 〈고대 제단 복원〉 연구가 끝나면 호박 코어를 보내 준다(코어를 잃으면 재전달).
            var amberCore = new QuestPart_ViviAmberCore()
            {
                inSignalEnable = quest.InitiateSignal,
            };
            if (slate.Get<bool>("coreDelivered"))
            {
                amberCore.MarkDelivered();
            }
            quest.AddPart(amberCore);

            // 5단계 대조율 의식 완료 → 성공. 곧바로 〈만개하는 여명〉이 이어지므로 표준 완료 편지는 보내지 않는다.
            quest.End(QuestEndOutcome.Success, inSignal: QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.GrandAttunementCompleted));
        }
    }
}
