using RimWorld;
using RimWorld.Planet;
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

        private readonly struct SignalSet
        {
            public readonly string AnchorTag;
            public readonly string AnchorMapTag;
            public readonly string BloomStarted;
            public readonly string BreathEcho;
            public readonly string BloomCondensed;
            public readonly string CalmEnded;
            public readonly string ApocryphaManifesting;
            public readonly string ApocryphaArrived;
            public readonly string ApocryphaDissolved;
            public readonly string ApocryphaDefeated;
            public readonly string CoronationCompleted;
            public readonly string EndingFinished;
            public readonly string BloomFailed;
            public readonly string Stolen;
            public readonly string AnchorLost;
            public readonly string ResonatorFallen;

            public SignalSet(Quest quest, bool hasMapParent)
            {
                string For(string name) => ViviEndingSignals.For(quest, name);

                AnchorTag = For(ViviEndingSignals.AnchorTag);
                AnchorMapTag = hasMapParent ? For(ViviEndingSignals.AnchorMapTag) : null;
                BloomStarted = For(ViviEndingSignals.BloomStarted);
                BreathEcho = For(ViviEndingSignals.BreathEcho);
                BloomCondensed = For(ViviEndingSignals.BloomCondensed);
                CalmEnded = For(ViviEndingSignals.CalmEnded);
                ApocryphaManifesting = For(ViviEndingSignals.ApocryphaManifesting);
                ApocryphaArrived = For(ViviEndingSignals.ApocryphaArrived);
                ApocryphaDissolved = For(ViviEndingSignals.ApocryphaDissolved);
                ApocryphaDefeated = For(ViviEndingSignals.ApocryphaDefeated);
                CoronationCompleted = For(ViviEndingSignals.CoronationCompleted);
                EndingFinished = For(ViviEndingSignals.EndingFinished);
                BloomFailed = For(ViviEndingSignals.BloomFailed);
                Stolen = For(ViviEndingSignals.EverflowerStolen);
                AnchorLost = For(ViviEndingSignals.AnchorLost);
                ResonatorFallen = For(ViviEndingSignals.ResonatorFallen);
            }
        }

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

            DetachTags(quest, anchor, anchor?.MapHeld?.Parent);
        }

        // 실패 시 개화 부품의 부수 효과와 퀘스트 태그를 즉시 걷어 낸다. 부품 제거는 신호 전파가 끝난 뒤 Remove로 수행한다.
        public static void CleanupAndDetachTags(Quest quest, ArcanePlant_Everflower anchor, MapParent anchorMapParent)
        {
            ForEachBloomPart(quest, part => ViviEndingUtility.RunStep($"bloom cleanup: {part.GetType().Name}", part.Cleanup));
            DetachTags(quest, anchor, anchorMapParent);
        }

        public static void Remove(Quest quest)
        {
            ForEachBloomPart(quest, quest.RemovePart);
        }

        private static void ForEachBloomPart(Quest quest, Action<QuestPart> action)
        {
            if (quest == null) { return; }

            var parts = quest.PartsListForReading;
            for (int i = parts.Count - 1; i >= 0; --i)
            {
                if (IsRootPart(parts[i])) { continue; }

                action(parts[i]);
            }
        }

        private static bool IsRootPart(QuestPart part)
        {
            return part is QuestPart_ViviEndingJourney
                || (part is QuestPart_QuestEnd end && end.outcome == QuestEndOutcome.Success);
        }

        private static void DetachTags(Quest quest, ArcanePlant_Everflower anchor, MapParent anchorMapParent)
        {
            if (quest == null) { return; }

            var anchorTag = ViviEndingSignals.For(quest, ViviEndingSignals.AnchorTag);
            var anchorMapTag = ViviEndingSignals.For(quest, ViviEndingSignals.AnchorMapTag);

            anchor?.questTags?.Remove(anchorTag);
            anchorMapParent?.questTags?.Remove(anchorMapTag);
            anchor?.MapHeld?.Parent?.questTags?.Remove(anchorMapTag);
        }

        private static void Add(Quest quest, ArcanePlant_Everflower anchor, float quality, Pawn resonator)
        {
            var mapParent = anchor.MapHeld?.Parent;
            var altar = ViviEndingUtility.AltarOf(anchor);
            var signals = new SignalSet(quest, mapParent != null);

            AttachTags(anchor, mapParent, signals);
            AddStatePart(quest, anchor, mapParent, signals);
            AddSiegeParts(quest, anchor, quality, signals);
            AddCalmAndBossParts(quest, anchor, signals);
            AddCoronationAndFinaleParts(quest, anchor, signals);
            AddFailureLetters(quest, altar, resonator, signals);
        }

        private static void AttachTags(ArcanePlant_Everflower anchor, MapParent mapParent, in SignalSet signals)
        {
            QuestUtility.AddQuestTag(ref anchor.questTags, signals.AnchorTag);

            if (mapParent != null)
            {
                QuestUtility.AddQuestTag(ref mapParent.questTags, signals.AnchorMapTag);
            }
        }

        private static void AddStatePart(Quest quest, ArcanePlant_Everflower anchor, MapParent mapParent, in SignalSet signals)
        {
            quest.AddPart(new QuestPart_ViviEndingState()
            {
                anchor = anchor,
                anchorMapParent = mapParent,
                inSignalBloomCondensed = signals.BloomCondensed,
                inSignalApocryphaArrived = signals.ApocryphaArrived,
                inSignalApocryphaDefeated = signals.ApocryphaDefeated,
                inSignalCoronationCompleted = signals.CoronationCompleted,
                inSignalStolen = signals.Stolen,
                inSignalAnchorLost = signals.AnchorLost,
                inSignalResonatorFallen = signals.ResonatorFallen,
                inSignalAnchorDestroyed = signals.AnchorTag + ".Destroyed",
                inSignalAnchorMapRemoved = signals.AnchorMapTag != null ? signals.AnchorMapTag + ".MapRemoved" : null,
                outSignalAnchorLost = signals.AnchorLost,
                outSignalBloomFailed = signals.BloomFailed,
            });
        }

        private static void AddSiegeParts(Quest quest, ArcanePlant_Everflower anchor, float quality, in SignalSet signals)
        {
            var federation = ViviEndingUtility.FindFederation(requireNonHostile: true);
            if (federation != null)
            {
                quest.AddPart(new QuestPart_ViviFederationAlliance()
                {
                    inSignal = signals.BloomStarted,
                    labelKey = LocalizeString_Letter.VV_Letter_EndingSiegeLabel,
                    textKey = LocalizeString_Letter.VV_Letter_EndingSiege,
                    letterDef = LetterDefOf.ThreatBig,
                    anchor = anchor,
                    faction = federation,
                });

                quest.AddPart(new QuestPart_FactionGoodwillLocked()
                {
                    inSignalEnable = signals.BloomStarted,
                    inSignalDisable = signals.BloomFailed,
                    faction1 = Faction.OfPlayer,
                    faction2 = federation,
                });
            }
            else
            {
                AddResonatorLetter(quest, signals.BloomStarted, LocalizeString_Letter.VV_Letter_EndingSiegeLabel, LocalizeString_Letter.VV_Letter_EndingSiege, LetterDefOf.ThreatBig, anchor);
            }

            AddDelay(quest, signals.BloomStarted, signals.BloomFailed, BreathEchoDelayTicks.RandomInRange, signals.BreathEcho);
            AddResonatorLetter(quest, signals.BreathEcho, LocalizeString_Letter.VV_Letter_EverflowerEcho5Label, LocalizeString_Letter.VV_Letter_EverflowerEcho5, LetterDefOf.NeutralEvent, anchor);

            var siegeTicks = Mathf.RoundToInt(Mathf.Lerp(SiegeMaxDurationTicks, SiegeMinDurationTicks, Mathf.Clamp01(quality / RitualMaxQuality)));
            var bloom = new QuestPart_ViviBloomCondensation()
            {
                inSignalEnable = signals.BloomStarted,
                inSignalDisable = signals.BloomFailed,
                anchor = anchor,
                durationTicks = siegeTicks,
            };
            bloom.outSignalsCompleted.Add(signals.BloomCondensed);
            quest.AddPart(bloom);

            quest.AddPart(new QuestPart_ViviFederationAid()
            {
                inSignalEnable = signals.BloomStarted,
                inSignalDisable = signals.BloomCondensed,
                anchor = anchor,
            });
        }

        private static void AddCalmAndBossParts(Quest quest, ArcanePlant_Everflower anchor, in SignalSet signals)
        {
            quest.AddPart(new QuestPart_ViviCalmBegin()
            {
                inSignal = signals.BloomCondensed,
                anchor = anchor,
            });

            AddResonatorLetter(quest, signals.BloomCondensed, LocalizeString_Letter.VV_Letter_EndingCalmLabel, LocalizeString_Letter.VV_Letter_EndingCalm, LetterDefOf.NeutralEvent, anchor);
            AddDelay(quest, signals.BloomCondensed, signals.BloomFailed, CalmDurationTicks.RandomInRange, signals.CalmEnded, CalmDelayLabel);

            quest.AddPart(new QuestPart_ViviAnchorWatch()
            {
                inSignalEnable = signals.BloomStarted,
                inSignalDisable = signals.BloomFailed,
                anchor = anchor,
                inSignalBloomCondensed = signals.BloomCondensed,
                outSignalAnchorLost = signals.AnchorLost,
            });

            var apocrypha = new QuestPart_ViviApocrypha()
            {
                inSignalEnable = signals.CalmEnded,
                inSignalDisable = signals.BloomFailed,
                anchor = anchor,
                inSignalDissolved = signals.ApocryphaDissolved,
                outSignalManifesting = signals.ApocryphaManifesting,
                outSignalArrived = signals.ApocryphaArrived,
            };
            apocrypha.outSignalsCompleted.Add(signals.ApocryphaDefeated);
            quest.AddPart(apocrypha);

            AddStaticLetter(
                quest,
                signals.ApocryphaManifesting,
                LocalizeString_Letter.VV_Letter_EndingApocryphaLabel,
                LocalizeString_Letter.VV_Letter_EndingApocrypha.Translate(),
                LetterDefOf.ThreatBig,
                LookTargets.Invalid);
        }

        private static void AddCoronationAndFinaleParts(Quest quest, ArcanePlant_Everflower anchor, in SignalSet signals)
        {
            quest.AddPart(new QuestPart_ViviEndingRecord()
            {
                inSignal = signals.ApocryphaDefeated,
                textKey = LocalizeString_Etc.VV_Quest_EndingTruthLog,
            });

            AddResonatorLetter(
                quest,
                signals.ApocryphaDefeated,
                LocalizeString_Letter.VV_Letter_EndingCoronationLabel,
                LocalizeString_Letter.VV_Letter_EndingCoronation,
                LetterDefOf.PositiveEvent,
                anchor);

            var finale = new QuestPart_ViviEndingFinale()
            {
                inSignalEnable = signals.CoronationCompleted,
                inSignalDisable = signals.BloomFailed,
                anchor = anchor,
            };
            finale.outSignalsCompleted.Add(signals.EndingFinished);
            quest.AddPart(finale);
        }

        private static void AddFailureLetters(Quest quest, Building_EverflowerAltar altar, Pawn resonator, in SignalSet signals)
        {
            var targets = altar != null ? new LookTargets(altar) : LookTargets.Invalid;

            AddStaticLetter(
                quest,
                signals.Stolen,
                LocalizeString_Letter.VV_Letter_EndingFailureLabel,
                LocalizeString_Letter.VV_Letter_EndingFailureStolen.Translate(),
                LetterDefOf.NegativeEvent,
                targets);

            var fallenText = ViviEndingUtility.TranslateWithResonator(LocalizeString_Letter.VV_Letter_EndingFailureResonator, resonator).Resolve();
            AddStaticLetter(
                quest,
                signals.ResonatorFallen,
                LocalizeString_Letter.VV_Letter_EndingFailureResonatorLabel,
                fallenText,
                LetterDefOf.NegativeEvent,
                targets);

            AddStaticLetter(
                quest,
                signals.AnchorLost,
                LocalizeString_Letter.VV_Letter_EndingFailureLostLabel,
                LocalizeString_Letter.VV_Letter_EndingFailureLost.Translate(),
                LetterDefOf.NegativeEvent,
                targets);
        }

        private static void AddStaticLetter(Quest quest, string inSignal, string labelKey, TaggedString text, LetterDef letterDef, LookTargets targets)
        {
            quest.AddPart(new QuestPart_Letter()
            {
                inSignal = inSignal,
                letter = LetterMaker.MakeLetter(labelKey.Translate(), text, letterDef, targets, null, quest),
            });
        }

        private static void AddResonatorLetter(Quest quest, string inSignal, string labelKey, string textKey, LetterDef letterDef, ArcanePlant_Everflower anchor)
        {
            quest.AddPart(new QuestPart_ViviEndingLetter()
            {
                inSignal = inSignal,
                labelKey = labelKey,
                textKey = textKey,
                letterDef = letterDef,
                anchor = anchor,
            });
        }

        private static void AddDelay(Quest quest, string inSignalEnable, string inSignalDisable, int delayTicks, string outSignalCompleted, string debugLabel = null)
        {
            var delay = new QuestPart_Delay()
            {
                inSignalEnable = inSignalEnable,
                inSignalDisable = inSignalDisable,
                delayTicks = delayTicks,
                debugLabel = debugLabel,
            };
            delay.outSignalsCompleted.Add(outSignalCompleted);
            quest.AddPart(delay);
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
