using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public partial class ViviEndingQuest
    {
        private const float DebugRitualQuality = 5f;

        private IEnumerable<Gizmo> GetDebugGizmos(ArcanePlant_Everflower everflower)
        {
            if (!DebugSettings.godMode || everflower.Faction != Faction.OfPlayer) { yield break; }

            if (_anchor != null && !_anchor.Destroyed && !IsAnchor(everflower)) { yield break; }

            var stage = Stage;
            var finished = IsFinished;

            if (!IsAltarUnlocked)
            {
                yield return DevCommand("DEV: Start missive quest now", () => StartMissiveQuest(everflower));
            }

            if (_lastEchoStage < MaxEchoStage)
            {
                yield return DevCommand($"DEV: Send echo letter {_lastEchoStage + 1}", () => DebugSendNextEchoLetter(everflower));
            }

            if (_lastEchoStage > 0)
            {
                yield return DevCommand("DEV: Reset echo letters", () => DebugSetLastEchoStage(0));
            }

            if (IsAltarUnlocked && !finished)
            {
                yield return DevCommand("DEV: Request amber core quest now", DebugRequestAmberCoreQuest);
            }

            if (!finished)
            {
                // 열거형 값 1이 비어 있으므로 None 다음은 AltarUnlocked로 직접 지정한다.
                var next = stage == ViviEndingStage.None ? ViviEndingStage.AltarUnlocked : stage + 1;
                yield return DevCommand($"DEV: Ending stage → {next}", () => DebugSetStage(next, everflower));
            }

            if (stage == ViviEndingStage.Siege)
            {
                yield return DevCommand("DEV: Bloom +10%", () => DebugSetBloom(BloomProgress + 0.1f));
                yield return DevCommand("DEV: Bloom → 99%", () => DebugSetBloom(QuestPart_ViviBloomCondensation.LockedProgress));
            }

            if (stage == ViviEndingStage.Calm)
            {
                yield return DevCommand("DEV: End calm now", DebugEndCalm);
            }

            if (!finished && stage != ViviEndingStage.Coronation && Boss == null)
            {
                var manifesting = ApocryphaPart != null && ApocryphaPart.IsManifesting;
                yield return DevCommand(manifesting ? "DEV: Skip Apocrypha intro" : "DEV: Spawn Apocrypha now", () => DebugSpawnBossNow(everflower));
            }

            if (!finished)
            {
                yield return DevCommand("DEV: Complete ending", () => DebugSetStage(ViviEndingStage.Completed, everflower));
                yield return DevCommand("DEV: Fail ending", () => DebugSetStage(ViviEndingStage.Failed, everflower));
            }
        }

        public void DebugSetStage(ViviEndingStage stage)
        {
            DebugSetStage(stage, null);
        }

        public void DebugSetLastEchoStage(int stage)
        {
            _lastEchoStage = Mathf.Clamp(stage, 0, MaxEchoStage);
        }

        internal void DebugSendNextEchoLetter(ArcanePlant_Everflower everflower)
        {
            if (_lastEchoStage >= MaxEchoStage) { return; }

            var resonator = everflower?.EverflowerComp.LinkedPawns.FirstOrDefault(v => v.Spawned && v.IsRoyalVivi());
            if (resonator == null)
            {
                Messages.Message("DEV: no linked royal vivi on the map to receive the echo letter.", everflower, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            SendEchoLetter(_lastEchoStage + 1, everflower, resonator);
        }

        public void DebugSetBloom(float value)
        {
            if (Stage != ViviEndingStage.Siege) { return; }

            BloomPart?.DebugSetProgress(value);
        }

        public void DebugSpawnBossNow()
        {
            DebugSpawnBossNow(null);
        }

        internal void DebugSetStage(ViviEndingStage stage, ArcanePlant_Everflower context)
        {
            switch (stage)
            {
                case ViviEndingStage.None:
                    DebugReset();
                    break;

                case ViviEndingStage.Failed:
                    if (IsFinished)
                    {
                        Log.Warning($"[ViViRace] ending debug: the ending already finished ({Stage}); not failing it.");
                        break;
                    }

                    if (DebugAdvanceTo(ViviEndingStage.Siege, context))
                    {
                        ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.AnchorLost);
                    }
                    break;

                default:
                    DebugAdvanceTo(stage, context);
                    break;
            }
        }

        internal void DebugSpawnBossNow(ArcanePlant_Everflower context)
        {
            if (Boss != null)
            {
                Messages.Message("DEV: Apocrypha is already on the map.", Boss, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (!DebugAdvanceTo(ViviEndingStage.Calm, context)) { return; }

            if (Stage == ViviEndingStage.Calm && CalmPart != null && CalmPart.State == QuestPartState.Enabled)
            {
                CalmPart.DebugForceComplete();
            }

            var apocrypha = ApocryphaPart;
            if (apocrypha == null || apocrypha.State != QuestPartState.Enabled)
            {
                Messages.Message($"DEV: Apocrypha cannot be spawned at ending stage {Stage}.", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            apocrypha.DebugSpawnNow(skipIntro: false);
        }

        private void DebugEndCalm()
        {
            if (Stage != ViviEndingStage.Calm) { return; }

            if (CalmPart != null && CalmPart.State == QuestPartState.Enabled)
            {
                CalmPart.DebugForceComplete();
            }
            else
            {
                ApocryphaPart?.DebugRetryNow();
            }
        }

        private void DebugRequestAmberCoreQuest()
        {
            if (RequestAmberCoreQuest(ViviEndingUtility.FindFederation(requireNonHostile: false)) == null)
            {
                Messages.Message("DEV: no quest could be generated for the ancient amber core.", MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        private bool DebugAdvanceTo(ViviEndingStage target, ArcanePlant_Everflower context)
        {
            for (int guard = 0; guard < 10; ++guard)
            {
                var stage = Stage;
                if (stage == ViviEndingStage.Failed) { return false; }
                if (stage >= target) { return true; }

                bool advanced;
                try
                {
                    advanced = DebugStep(stage, target, context);
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] ending debug: failed to advance from {stage}: {ex}");
                    return false;
                }

                if (!advanced)
                {
                    Log.Warning($"[ViViRace] ending debug: could not advance from {stage} toward {target}.");
                    return false;
                }
            }

            return Stage >= target;
        }

        private bool DebugStep(ViviEndingStage stage, ViviEndingStage target, ArcanePlant_Everflower context)
        {
            switch (stage)
            {
                case ViviEndingStage.None:
                    StartMissiveQuest(context);
                    return _missiveQuest != null;

                case ViviEndingStage.AltarUnlocked:
                    {
                        var everflower = context != null && !context.Destroyed ? context : ViviEndingUtility.FindEndingCandidate();
                        if (everflower == null)
                        {
                            Log.Warning("[ViViRace] ending debug: no everflower to start the siege with.");
                            return false;
                        }

                        return StartBloomQuest(everflower, DebugRitualQuality);
                    }

                case ViviEndingStage.Siege:
                    BloomPart?.DebugSetProgress(QuestPart_ViviBloomCondensation.LockedProgress);
                    return Stage != ViviEndingStage.Siege;

                case ViviEndingStage.Calm:
                    {
                        if (CalmPart != null && CalmPart.State == QuestPartState.Enabled)
                        {
                            CalmPart.DebugForceComplete();
                        }

                        var apocrypha = ApocryphaPart;
                        if (apocrypha == null || apocrypha.State != QuestPartState.Enabled) { return false; }

                        if (target == ViviEndingStage.Boss)
                        {
                            apocrypha.DebugSpawnNow(skipIntro: true);
                        }
                        else
                        {
                            apocrypha.DebugDefeat();
                        }

                        return Stage != ViviEndingStage.Calm;
                    }

                case ViviEndingStage.Boss:
                    ApocryphaPart?.DebugDefeat();
                    return Stage != ViviEndingStage.Boss;

                case ViviEndingStage.Coronation:
                    ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.CoronationCompleted, ViviEndingUtility.ResonatorOf(_anchor).Named("PAWN"));
                    return Stage != ViviEndingStage.Coronation;

                default:
                    return false;
            }
        }

        private void DebugReset()
        {
            foreach (var quest in new[] { _bloomQuest, _missiveQuest })
            {
                if (quest == null) { continue; }

                if (!quest.Historical)
                {
                    quest.End(QuestEndOutcome.Unknown, sendLetter: false, playSound: false);
                }

                if (Find.QuestManager.Contains(quest))
                {
                    Find.QuestManager.Remove(quest);
                }

                ClearQuestReferences(quest);
            }

            ViviEndingUtility.RunStep("debug reset: cleanup gimmicks", ViviEndingUtility.CleanupGimmicks);
            ViviEndingUtility.RunStep("debug reset: end ominous silence", ViviEndingUtility.EndOminousSilence);
            ViviEndingUtility.RunStep("debug reset: end golden bloom", ViviEndingUtility.EndGoldenBloom);

            _missiveQuest = null;
            _bloomQuest = null;
            _anchor = null;
            _cachedQuest = null;
            _cachedPartCount = -1;
        }

        // 삭제한 퀘스트의 참조를 편지·메시지·컨디션에 남겨 두면 불러올 때 "Could not resolve reference" 경고가 난다.
        private static void ClearQuestReferences(Quest quest)
        {
            foreach (var archivable in Find.Archive.ArchivablesListForReading)
            {
                if (archivable is ChoiceLetter letter && letter.quest == quest)
                {
                    letter.quest = null;
                }
                else if (archivable is Message message && message.quest == quest)
                {
                    message.quest = null;
                }
            }

            foreach (var letter in Find.LetterStack.LettersListForReading)
            {
                if (letter is ChoiceLetter choiceLetter && choiceLetter.quest == quest)
                {
                    choiceLetter.quest = null;
                }
            }

            foreach (var map in Find.Maps)
            {
                foreach (var condition in map.gameConditionManager.ActiveConditions)
                {
                    if (condition.quest == quest)
                    {
                        condition.quest = null;
                    }
                }
            }
        }

        private static Command_Action DevCommand(string label, Action action)
        {
            return new Command_Action()
            {
                defaultLabel = label,
                action = action,
            };
        }
    }
}
