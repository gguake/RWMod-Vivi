using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace VVRace
{
    // 〈만개하는 여명〉 퀘스트 생성. 5단계 대조율 의식이 끝나는 순간 창구(ViviEndingQuest)가 만든다.
    // 슬레이트: anchor(의식을 치른 영원꽃), map(앵커 맵), quality(의식 품질 0~10).
    // 단계는 신호로만 바뀐다: 시작 → BloomCondensed → CalmEnded → ApocryphaArrived → ApocryphaDefeated → CoronationCompleted → EndingFinished,
    // 실패는 EverflowerStolen / AnchorLost.
    // 같은 신호를 받는 부품은 "상태 → 처리 → 편지 → QuestEnd" 순서로 추가한다. QuestEnd가 먼저 처리되면 퀘스트가 끝나 뒤 부품이 신호를 받지 못한다.
    public class QuestNode_Root_ViviBloom : QuestNode
    {
        private const int SiegeMaxDurationTicks = 600000;   // 의식 품질 0 → 10일
        private const int SiegeMinDurationTicks = 480000;   // 의식 최대 품질 → 8일
        private const float RitualMaxQuality = 10f;
        private static readonly IntRange CalmDurationTicks = new IntRange(90000, 120000);

        // 평화 타이머(바닐라 QuestPart_Delay)를 창구가 찾는 표식
        public const string CalmDelayLabel = "VV_EndingCalm";

        protected override bool TestRunInt(Slate slate)
        {
            return slate.Get<ArcanePlant_Everflower>("anchor") != null;
        }

        protected override void RunInt()
        {
            var quest = QuestGen.quest;
            var slate = QuestGen.slate;

            var anchor = slate.Get<ArcanePlant_Everflower>("anchor");
            var map = slate.Get<Map>("map") ?? anchor.MapHeld;
            var mapParent = map?.Parent;
            var quality = slate.Get<float>("quality");
            var altar = ViviEndingUtility.AltarOf(anchor);

            slate.Set("resolvedQuestName", LocalizeString_EndingCore.VV_Quest_EndingName.Translate().Resolve());
            slate.Set("resolvedQuestDescription", LocalizeString_EndingCore.VV_Quest_EndingDesc.Translate().Resolve());

            // 앵커와 앵커 맵에 퀘스트 태그를 붙이면 바닐라가 "<태그>.Destroyed", "<태그>.MapRemoved"를 보낸다.
            var anchorTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID(ViviEndingSignals.AnchorTag);
            QuestUtility.AddQuestTag(ref anchor.questTags, anchorTag);

            string anchorMapTag = null;
            if (mapParent != null)
            {
                anchorMapTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID(ViviEndingSignals.AnchorMapTag);
                QuestUtility.AddQuestTag(ref mapParent.questTags, anchorMapTag);
            }

            var initiate = quest.InitiateSignal;
            var bloomCondensed = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.BloomCondensed);
            var calmEnded = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.CalmEnded);
            var apocryphaArrived = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.ApocryphaArrived);
            var apocryphaDissolved = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.ApocryphaDissolved);
            var apocryphaDefeated = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.ApocryphaDefeated);
            var coronationCompleted = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.CoronationCompleted);
            var endingFinished = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.EndingFinished);
            var stolen = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.EverflowerStolen);
            var anchorLost = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.AnchorLost);

            // 0) 진행 단계·앵커 소실 감지. 모든 신호를 가장 먼저 받도록 첫 부품으로 둔다.
            quest.AddPart(new QuestPart_ViviEndingState()
            {
                anchor = anchor,
                inSignalBloomCondensed = bloomCondensed,
                inSignalApocryphaArrived = apocryphaArrived,
                inSignalApocryphaDefeated = apocryphaDefeated,
                inSignalCoronationCompleted = coronationCompleted,
                inSignalStolen = stolen,
                inSignalAnchorLost = anchorLost,
                inSignalAnchorDestroyed = anchorTag + ".Destroyed",
                inSignalAnchorMapRemoved = anchorMapTag != null ? anchorMapTag + ".MapRemoved" : null,
                outSignalAnchorLost = anchorLost,
            });

            // 1) 〈여명의 맥동〉 공성: 응축 0→99%(8~10일), 연방 지원군. 습격은 습격 비콘(EndingCorePatch)으로 스토리텔러가 보낸다.
            var siegeTicks = Mathf.RoundToInt(Mathf.Lerp(SiegeMaxDurationTicks, SiegeMinDurationTicks, Mathf.Clamp01(quality / RitualMaxQuality)));
            var siegeDays = (siegeTicks / (float)GenDate.TicksPerDay).ToString("0.#");
            var siegeText = LocalizeString_EndingCore.VV_Letter_EndingSiege.Translate(siegeDays.Named("DAYS")).Resolve();
            if (ViviEndingUtility.FindFederation(requireNonHostile: true) != null)
            {
                siegeText += "\n\n" + LocalizeString_EndingCore.VV_Letter_EndingSiegeFederation.Translate().Resolve();
            }

            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = initiate,
                letter = LetterMaker.MakeLetter(LocalizeString_EndingCore.VV_Letter_EndingSiegeLabel.Translate(), siegeText, LetterDefOf.ThreatBig, new LookTargets(anchor), null, quest),
            });

            var bloom = new QuestPart_ViviBloomCondensation()
            {
                inSignalEnable = initiate,
                anchor = anchor,
                durationTicks = siegeTicks,
            };
            bloom.outSignalsCompleted.Add(bloomCondensed);
            quest.AddPart(bloom);

            quest.AddPart(new QuestPart_ViviFederationAid()
            {
                inSignalEnable = initiate,
                inSignalDisable = bloomCondensed,
                anchor = anchor,
            });

            // 2) 〈폭풍 전의 고요〉 평화 기간: 맵 정리, 편지, 타이머, 〈불길한 정적〉, 엔딩까지 이어지는 〈황금빛 꽃안개〉
            var calmTicks = CalmDurationTicks.RandomInRange;
            quest.AddPart(new QuestPart_ViviCalmBegin()
            {
                inSignal = bloomCondensed,
                anchor = anchor,
            });

            quest.AddPart(new QuestPart_ViviEndingLetter()
            {
                inSignal = bloomCondensed,
                labelKey = LocalizeString_EndingCore.VV_Letter_EndingCalmLabel,
                textKey = LocalizeString_EndingCore.VV_Letter_EndingCalm,
                guideKey = LocalizeString_EndingCore.VV_Letter_EndingCalmGuide,
                guideDays = (calmTicks / (float)GenDate.TicksPerDay).ToString("0.#"),
                letterDef = LetterDefOf.NeutralEvent,
                anchor = anchor,
            });

            var calm = new QuestPart_Delay()
            {
                inSignalEnable = bloomCondensed,
                delayTicks = calmTicks,
                expiryInfoPart = LocalizeString_EndingCore.VV_Quest_EndingCalmExpiry.Translate(),
                expiryInfoPartTip = LocalizeString_EndingCore.VV_Quest_EndingCalmExpiryTip.Translate(),
                debugLabel = CalmDelayLabel,
            };
            calm.outSignalsCompleted.Add(calmEnded);
            quest.AddPart(calm);

            if (mapParent != null)
            {
                if (EndingCoreDefOf.VV_OminousSilence != null)
                {
                    quest.AddPart(new QuestPart_GameCondition()
                    {
                        inSignal = bloomCondensed,
                        gameCondition = GameConditionMaker.MakeCondition(EndingCoreDefOf.VV_OminousSilence, calmTicks),
                        mapParent = mapParent,
                        sendStandardLetter = false,
                    });
                }

                if (EndingCoreDefOf.VV_GoldenBloom != null && GameConditionMaker.MakeCondition(EndingCoreDefOf.VV_GoldenBloom) is GameCondition_GoldenBloom goldenBloom)
                {
                    goldenBloom.Permanent = true;
                    goldenBloom.fromEnding = true;
                    quest.AddPart(new QuestPart_GameCondition()
                    {
                        inSignal = bloomCondensed,
                        gameCondition = goldenBloom,
                        mapParent = mapParent,
                        sendStandardLetter = false,
                    });
                }
            }

            // 3) 〈바스러진 여왕, 아포크리파〉: 등장(진입 칸이 없으면 보류·재시도), 소멸 연출이 끝나면 토벌
            var apocrypha = new QuestPart_ViviApocrypha()
            {
                inSignalEnable = calmEnded,
                anchor = anchor,
                inSignalDissolved = apocryphaDissolved,
                outSignalArrived = apocryphaArrived,
            };
            apocrypha.outSignalsCompleted.Add(apocryphaDefeated);
            quest.AddPart(apocrypha);

            // 등장 편지는 신호의 SUBJECT(보스)를 바라본다.
            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = apocryphaArrived,
                letter = LetterMaker.MakeLetter(LocalizeString_EndingCore.VV_Letter_EndingApocryphaLabel.Translate(), LocalizeString_EndingCore.VV_Letter_EndingApocrypha.Translate(), LetterDefOf.ThreatBig, LookTargets.Invalid, null, quest),
            });

            // 4) 토벌 → 응축 100%, 〈여왕의 대관식〉 대기
            quest.AddPart(new QuestPart_ViviEndingLetter()
            {
                inSignal = apocryphaDefeated,
                labelKey = LocalizeString_EndingCore.VV_Letter_EndingCoronationLabel,
                textKey = LocalizeString_EndingCore.VV_Letter_EndingCoronation,
                letterDef = LetterDefOf.PositiveEvent,
                anchor = anchor,
            });

            // 5) 〈영원한 봄의 제국〉: 대관식 → 6단계 개화·연출 → 엔딩 편지와 크레딧 → 성공(엔딩 편지가 있으므로 표준 완료 편지는 보내지 않는다)
            var finale = new QuestPart_ViviEndingFinale()
            {
                inSignalEnable = coronationCompleted,
                anchor = anchor,
            };
            finale.outSignalsCompleted.Add(endingFinished);
            quest.AddPart(finale);
            quest.End(QuestEndOutcome.Success, inSignal: endingFinished);

            // 6) 실패: 정리는 상태·아포크리파 부품이 퀘스트 종료 정리(Cleanup)에서 한다.
            var failureTargets = altar != null ? new LookTargets(altar) : LookTargets.Invalid;
            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = stolen,
                letter = LetterMaker.MakeLetter(LocalizeString_EndingCore.VV_Letter_EndingFailureLabel.Translate(), LocalizeString_EndingCore.VV_Letter_EndingFailureStolen.Translate(), LetterDefOf.NegativeEvent, failureTargets, null, quest),
            });
            quest.End(QuestEndOutcome.Fail, inSignal: stolen);

            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = anchorLost,
                letter = LetterMaker.MakeLetter(LocalizeString_EndingCore.VV_Letter_EndingFailureLostLabel.Translate(), LocalizeString_EndingCore.VV_Letter_EndingFailureLost.Translate(), LetterDefOf.NegativeEvent, failureTargets, null, quest),
            });
            quest.End(QuestEndOutcome.Fail, inSignal: anchorLost);
        }
    }
}
