using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace VVRace
{
    // 같은 신호를 받는 부품은 상태 → 처리 → 편지 → QuestEnd 순서로 추가한다. 퀘스트가 먼저 끝나면 뒤 부품은 신호를 받지 못한다.
    // ViviEndingQuest.GenerateQuest는 마지막 AnchorLost 실패 QuestEnd의 존재로 생성 완료를 확인한다.
    public class QuestNode_Root_ViviBloom : QuestNode
    {
        private const int SiegeMaxDurationTicks = 600000;
        private const int SiegeMinDurationTicks = 480000;
        private const float RitualMaxQuality = 10f;
        private static readonly IntRange CalmDurationTicks = new IntRange(90000, 120000);

        // ViviEndingQuest에서 평화 타이머(QuestPart_Delay)를 식별하는 표식.
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
            var resonator = slate.Get<Pawn>("resonator");
            var altar = ViviEndingUtility.AltarOf(anchor);

            slate.Set("resolvedQuestName", LocalizeString_Etc.VV_Quest_EndingName.Translate().Resolve());
            slate.Set("resolvedQuestDescription", LocalizeString_Etc.VV_Quest_EndingDesc.Translate().Resolve());

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
            var apocryphaManifesting = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.ApocryphaManifesting);
            var apocryphaArrived = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.ApocryphaArrived);
            var apocryphaDissolved = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.ApocryphaDissolved);
            var apocryphaDefeated = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.ApocryphaDefeated);
            var coronationCompleted = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.CoronationCompleted);
            var endingFinished = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.EndingFinished);
            var stolen = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.EverflowerStolen);
            var anchorLost = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.AnchorLost);
            var resonatorFallen = QuestGenUtility.HardcodedSignalWithQuestID(ViviEndingSignals.ResonatorFallen);

            quest.AddPart(new QuestPart_ViviEndingState()
            {
                anchor = anchor,
                inSignalBloomCondensed = bloomCondensed,
                inSignalApocryphaArrived = apocryphaArrived,
                inSignalApocryphaDefeated = apocryphaDefeated,
                inSignalCoronationCompleted = coronationCompleted,
                inSignalStolen = stolen,
                inSignalAnchorLost = anchorLost,
                inSignalResonatorFallen = resonatorFallen,
                inSignalAnchorDestroyed = anchorTag + ".Destroyed",
                inSignalAnchorMapRemoved = anchorMapTag != null ? anchorMapTag + ".MapRemoved" : null,
                outSignalAnchorLost = anchorLost,
            });

            var siegeTicks = Mathf.RoundToInt(Mathf.Lerp(SiegeMaxDurationTicks, SiegeMinDurationTicks, Mathf.Clamp01(quality / RitualMaxQuality)));
            var federation = ViviEndingUtility.FindFederation(requireNonHostile: true);
            var siegeText = LocalizeString_Letter.VV_Letter_EndingSiege.Translate().Resolve();
            if (federation != null)
            {
                siegeText += "\n\n" + LocalizeString_Letter.VV_Letter_EndingSiegeFederation.Translate().Resolve();
            }

            // 개화 시작 시 연방을 동맹으로 올리고 퀘스트가 끝날 때까지 우호도를 잠근다(지원군 사망 등으로 깎이지 않도록).
            if (federation != null)
            {
                quest.AddPart(new QuestPart_ViviFederationAlliance()
                {
                    inSignal = initiate,
                    faction = federation,
                });

                quest.AddPart(new QuestPart_FactionGoodwillLocked()
                {
                    inSignalEnable = initiate,
                    faction1 = Faction.OfPlayer,
                    faction2 = federation,
                });
            }

            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = initiate,
                letter = LetterMaker.MakeLetter(LocalizeString_Letter.VV_Letter_EndingSiegeLabel.Translate(), siegeText, LetterDefOf.ThreatBig, new LookTargets(anchor), null, quest),
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

            var calmTicks = CalmDurationTicks.RandomInRange;
            quest.AddPart(new QuestPart_ViviCalmBegin()
            {
                inSignal = bloomCondensed,
                anchor = anchor,
            });

            quest.AddPart(new QuestPart_ViviEndingLetter()
            {
                inSignal = bloomCondensed,
                labelKey = LocalizeString_Letter.VV_Letter_EndingCalmLabel,
                textKey = LocalizeString_Letter.VV_Letter_EndingCalm,
                letterDef = LetterDefOf.NeutralEvent,
                anchor = anchor,
            });

            var calm = new QuestPart_Delay()
            {
                inSignalEnable = bloomCondensed,
                delayTicks = calmTicks,
                debugLabel = CalmDelayLabel,
            };
            calm.outSignalsCompleted.Add(calmEnded);
            quest.AddPart(calm);

            // 생성 시점의 맵에 고정되는 QuestPart_GameCondition 대신 감시 부품이 현재 앵커 맵의 컨디션을 관리한다.
            // 평화 타이머가 먼저 활성화된 뒤 BloomCondensed를 받도록 타이머 다음에 추가한다.
            quest.AddPart(new QuestPart_ViviAnchorWatch()
            {
                inSignalEnable = initiate,
                anchor = anchor,
                inSignalBloomCondensed = bloomCondensed,
                outSignalAnchorLost = anchorLost,
            });

            var apocrypha = new QuestPart_ViviApocrypha()
            {
                inSignalEnable = calmEnded,
                anchor = anchor,
                inSignalDissolved = apocryphaDissolved,
                outSignalManifesting = apocryphaManifesting,
                outSignalArrived = apocryphaArrived,
            };
            apocrypha.outSignalsCompleted.Add(apocryphaDefeated);
            quest.AddPart(apocrypha);

            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = apocryphaManifesting,
                letter = LetterMaker.MakeLetter(LocalizeString_Letter.VV_Letter_EndingApocryphaLabel.Translate(), LocalizeString_Letter.VV_Letter_EndingApocrypha.Translate(), LetterDefOf.ThreatBig, LookTargets.Invalid, null, quest),
            });

            quest.AddPart(new QuestPart_ViviEndingLetter()
            {
                inSignal = apocryphaDefeated,
                labelKey = LocalizeString_Letter.VV_Letter_EndingCoronationLabel,
                textKey = LocalizeString_Letter.VV_Letter_EndingCoronation,
                letterDef = LetterDefOf.PositiveEvent,
                anchor = anchor,
            });

            var finale = new QuestPart_ViviEndingFinale()
            {
                inSignalEnable = coronationCompleted,
                anchor = anchor,
            };
            finale.outSignalsCompleted.Add(endingFinished);
            quest.AddPart(finale);
            quest.End(QuestEndOutcome.Success, inSignal: endingFinished);

            var failureTargets = altar != null ? new LookTargets(altar) : LookTargets.Invalid;
            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = stolen,
                letter = LetterMaker.MakeLetter(LocalizeString_Letter.VV_Letter_EndingFailureLabel.Translate(), LocalizeString_Letter.VV_Letter_EndingFailureStolen.Translate(), LetterDefOf.NegativeEvent, failureTargets, null, quest),
            });
            quest.End(QuestEndOutcome.Fail, inSignal: stolen);

            var fallenText = ViviEndingUtility.TranslateWithResonator(LocalizeString_Letter.VV_Letter_EndingFailureResonator, resonator).Resolve();
            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = resonatorFallen,
                letter = LetterMaker.MakeLetter(LocalizeString_Letter.VV_Letter_EndingFailureResonatorLabel.Translate(), fallenText, LetterDefOf.NegativeEvent, failureTargets, null, quest),
            });
            quest.End(QuestEndOutcome.Fail, inSignal: resonatorFallen);

            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = anchorLost,
                letter = LetterMaker.MakeLetter(LocalizeString_Letter.VV_Letter_EndingFailureLostLabel.Translate(), LocalizeString_Letter.VV_Letter_EndingFailureLost.Translate(), LetterDefOf.NegativeEvent, failureTargets, null, quest),
            });
            quest.End(QuestEndOutcome.Fail, inSignal: anchorLost);
        }
    }
}
