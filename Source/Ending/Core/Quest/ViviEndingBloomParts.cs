using RimWorld;
using System;
using Verse;

namespace VVRace
{
    // 만개 의식이 끝나면 진행 중인 에픽 퀘스트에 개화 이후 부품을 덧붙인다. QuestGen 밖이므로 신호 이름은 ViviEndingSignals.For로 만들고,
    // 모든 부품을 추가한 뒤에 BloomStarted를 보내 켠다.
    // 같은 신호를 받는 부품은 상태 → 처리 → 편지 → QuestEnd 순서로 추가한다. 퀘스트가 먼저 끝나면 뒤 부품은 신호를 받지 못한다.
    public static class ViviEndingBloomParts
    {
        private const int SiegeMaxDurationTicks = 600000;
        private const int SiegeMinDurationTicks = 480000;
        private const float RitualMaxQuality = 10f;
        private static readonly IntRange CalmDurationTicks = new IntRange(90000, 120000);
        private static readonly IntRange BreathEchoDelayTicks = new IntRange(1800, 2500);

        public const string CalmDelayLabel = "VV_EndingCalm";

        // 중간에 실패하면 덧붙인 부품과 퀘스트 태그를 걷어 내 다음 시도가 막히지 않게 한다.
        public static bool TryAdd(Quest quest, ArcanePlant_Everflower anchor, float quality, Pawn resonator)
        {
            var partCount = quest.PartsListForReading.Count;
            try
            {
                Add(quest, anchor, quality, resonator);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] failed to add the bloom parts to the ending quest; rolled them back: {ex}");
                ViviEndingUtility.RunStep("roll back bloom parts", () => Rollback(quest, anchor, partCount));
                return false;
            }
        }

        private static void Rollback(Quest quest, ArcanePlant_Everflower anchor, int partCount)
        {
            var parts = quest.PartsListForReading;
            for (int i = parts.Count - 1; i >= partCount; --i)
            {
                quest.RemovePart(parts[i]);
            }

            anchor.questTags?.Remove(ViviEndingSignals.For(quest, ViviEndingSignals.AnchorTag));
            anchor.MapHeld?.Parent?.questTags?.Remove(ViviEndingSignals.For(quest, ViviEndingSignals.AnchorMapTag));
        }

        private static void Add(Quest quest, ArcanePlant_Everflower anchor, float quality, Pawn resonator)
        {
            string Signal(string name) => ViviEndingSignals.For(quest, name);

            var map = anchor.MapHeld;
            var mapParent = map?.Parent;
            var altar = ViviEndingUtility.AltarOf(anchor);

            var anchorTag = Signal(ViviEndingSignals.AnchorTag);
            QuestUtility.AddQuestTag(ref anchor.questTags, anchorTag);

            string anchorMapTag = null;
            if (mapParent != null)
            {
                anchorMapTag = Signal(ViviEndingSignals.AnchorMapTag);
                QuestUtility.AddQuestTag(ref mapParent.questTags, anchorMapTag);
            }

            var bloomStarted = Signal(ViviEndingSignals.BloomStarted);
            var breathEcho = Signal(ViviEndingSignals.BreathEcho);
            var bloomCondensed = Signal(ViviEndingSignals.BloomCondensed);
            var calmEnded = Signal(ViviEndingSignals.CalmEnded);
            var apocryphaManifesting = Signal(ViviEndingSignals.ApocryphaManifesting);
            var apocryphaArrived = Signal(ViviEndingSignals.ApocryphaArrived);
            var apocryphaDissolved = Signal(ViviEndingSignals.ApocryphaDissolved);
            var apocryphaDefeated = Signal(ViviEndingSignals.ApocryphaDefeated);
            var coronationCompleted = Signal(ViviEndingSignals.CoronationCompleted);
            var endingFinished = Signal(ViviEndingSignals.EndingFinished);
            var stolen = Signal(ViviEndingSignals.EverflowerStolen);
            var anchorLost = Signal(ViviEndingSignals.AnchorLost);
            var resonatorFallen = Signal(ViviEndingSignals.ResonatorFallen);

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

                quest.AddPart(new QuestPart_ViviFederationAlliance()
                {
                    inSignal = bloomStarted,
                    faction = federation,
                });

                quest.AddPart(new QuestPart_FactionGoodwillLocked()
                {
                    inSignalEnable = bloomStarted,
                    faction1 = Faction.OfPlayer,
                    faction2 = federation,
                });
            }

            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = bloomStarted,
                letter = LetterMaker.MakeLetter(LocalizeString_Letter.VV_Letter_EndingSiegeLabel.Translate(), siegeText, LetterDefOf.ThreatBig, new LookTargets(anchor), null, quest),
            });

            var breath = new QuestPart_Delay()
            {
                inSignalEnable = bloomStarted,
                delayTicks = BreathEchoDelayTicks.RandomInRange,
            };
            breath.outSignalsCompleted.Add(breathEcho);
            quest.AddPart(breath);

            quest.AddPart(new QuestPart_ViviEndingLetter()
            {
                inSignal = breathEcho,
                labelKey = LocalizeString_Letter.VV_Letter_EverflowerEcho5Label,
                textKey = LocalizeString_Letter.VV_Letter_EverflowerEcho5,
                letterDef = LetterDefOf.NeutralEvent,
                anchor = anchor,
            });

            var bloom = new QuestPart_ViviBloomCondensation()
            {
                inSignalEnable = bloomStarted,
                anchor = anchor,
                durationTicks = siegeTicks,
            };
            bloom.outSignalsCompleted.Add(bloomCondensed);
            quest.AddPart(bloom);

            quest.AddPart(new QuestPart_ViviFederationAid()
            {
                inSignalEnable = bloomStarted,
                inSignalDisable = bloomCondensed,
                anchor = anchor,
            });

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
                delayTicks = CalmDurationTicks.RandomInRange,
                debugLabel = CalmDelayLabel,
            };
            calm.outSignalsCompleted.Add(calmEnded);
            quest.AddPart(calm);

            quest.AddPart(new QuestPart_ViviAnchorWatch()
            {
                inSignalEnable = bloomStarted,
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

            quest.AddPart(new QuestPart_ViviEndingRecord()
            {
                inSignal = apocryphaDefeated,
                textKey = LocalizeString_Etc.VV_Quest_EndingTruthLog,
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

            var failureTargets = altar != null ? new LookTargets(altar) : LookTargets.Invalid;
            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = stolen,
                letter = LetterMaker.MakeLetter(LocalizeString_Letter.VV_Letter_EndingFailureLabel.Translate(), LocalizeString_Letter.VV_Letter_EndingFailureStolen.Translate(), LetterDefOf.NegativeEvent, failureTargets, null, quest),
            });
            AddEnd(quest, QuestEndOutcome.Fail, stolen);

            var fallenText = ViviEndingUtility.TranslateWithResonator(LocalizeString_Letter.VV_Letter_EndingFailureResonator, resonator).Resolve();
            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = resonatorFallen,
                letter = LetterMaker.MakeLetter(LocalizeString_Letter.VV_Letter_EndingFailureResonatorLabel.Translate(), fallenText, LetterDefOf.NegativeEvent, failureTargets, null, quest),
            });
            AddEnd(quest, QuestEndOutcome.Fail, resonatorFallen);

            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = anchorLost,
                letter = LetterMaker.MakeLetter(LocalizeString_Letter.VV_Letter_EndingFailureLostLabel.Translate(), LocalizeString_Letter.VV_Letter_EndingFailureLost.Translate(), LetterDefOf.NegativeEvent, failureTargets, null, quest),
            });
            AddEnd(quest, QuestEndOutcome.Fail, anchorLost);
        }

        public static void AddEnd(Quest quest, QuestEndOutcome outcome, string inSignal)
        {
            quest.AddPart(new QuestPart_QuestEnd()
            {
                inSignal = inSignal,
                outcome = outcome,
            });
        }
    }
}
