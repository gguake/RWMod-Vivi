using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public partial class ViviEndingQuest
    {
        private const float DebugRitualQuality = 5f;

        private bool IsFinished => Stage is ViviEndingStage.Completed or ViviEndingStage.Failed;

        private IEnumerable<Gizmo> GetDebugGizmos(ArcanePlant_Everflower everflower)
        {
            if (!DebugSettings.godMode || everflower.Faction != Faction.OfPlayer) { yield break; }
            if (_anchor != null && !_anchor.Destroyed && !IsAnchor(everflower)) { yield break; }

            var stage = Stage;
            var finished = IsFinished;

            if ((stage == ViviEndingStage.None || stage == ViviEndingStage.Tales) && !_tales.AllDelivered)
            {
                yield return EndingVfxUtility.DevAction($"DEV: Deliver fairy tale {_tales.Delivered + 1}", () => DebugDeliverTale(everflower));
            }

            if (stage < ViviEndingStage.AltarUnlocked)
            {
                yield return EndingVfxUtility.DevAction("DEV: Unlock altar now", DebugUnlockAltar);
            }

            if (stage == ViviEndingStage.Ruins && _ruins.Investigated < ViviAncientRuins.EchoCount)
            {
                yield return EndingVfxUtility.DevAction(
                    $"DEV: Ruin echo {_ruins.Investigated + 1}",
                    () => _ruins.DebugInvestigateNext(ViviEndingUtility.ResonatorOf(everflower)));
            }

            if (!finished)
            {
                var next = stage + 1;
                yield return EndingVfxUtility.DevAction($"DEV: Ending stage → {next}", () => DebugSetStage(next, everflower));
            }

            if (stage == ViviEndingStage.Siege)
            {
                yield return EndingVfxUtility.DevAction("DEV: Bloom +10%", () => DebugSetBloom(BloomProgress + 0.1f));
                yield return EndingVfxUtility.DevAction("DEV: Bloom → 99%", () => DebugSetBloom(QuestPart_ViviBloomCondensation.LockedProgress));
            }

            if (stage == ViviEndingStage.Calm)
            {
                yield return EndingVfxUtility.DevAction("DEV: End calm now", DebugEndCalm);
            }

            if (!finished && stage >= ViviEndingStage.AltarUnlocked && stage != ViviEndingStage.Coronation && Boss == null)
            {
                var manifesting = ApocryphaPart != null && ApocryphaPart.IsManifesting;
                var label = manifesting ? "DEV: Skip Asphodel intro" : "DEV: Spawn Asphodel now";
                yield return EndingVfxUtility.DevAction(label, () => DebugSpawnBossNow(everflower));
            }

            if (!finished)
            {
                yield return EndingVfxUtility.DevAction("DEV: Complete ending", () => DebugSetStage(ViviEndingStage.Completed, everflower));
                yield return EndingVfxUtility.DevAction("DEV: Fail ending", () => DebugSetStage(ViviEndingStage.Failed, everflower));
            }
        }

        public void DebugSetStage(ViviEndingStage stage)
        {
            DebugSetStage(stage, null);
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

        // 편지·드롭 없이 동화책·밀서·유적을 마친 상태로 만든다(시험 장면용).
        public void DebugUnlockAltar()
        {
            if (Stage >= ViviEndingStage.AltarUnlocked) { return; }
            if (_quest == null && !CreateQuest(sendLetter: false)) { return; }

            _tales.DebugSetDelivered(ViviEndingTales.VolumeCount);
            _ruins.DebugComplete();
        }

        public void DebugDeliverTale(ArcanePlant_Everflower context)
        {
            if (_tales.AllDelivered) { return; }
            if (_quest == null && !CreateQuest()) { return; }

            var everflower = ResolveDebugEverflower(context);
            _tales.DeliverNext(everflower, ViviEndingUtility.FindFederation(requireNonHostile: false));
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
                        ViviEndingSignals.Send(_quest, ViviEndingSignals.AnchorLost);
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
                Messages.Message("DEV: Asphodel is already on the map.", Boss, MessageTypeDefOf.RejectInput, historical: false);
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
                Messages.Message($"DEV: Asphodel cannot be spawned at ending stage {Stage}.", MessageTypeDefOf.RejectInput, historical: false);
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

        private bool DebugAdvanceTo(ViviEndingStage target, ArcanePlant_Everflower context)
        {
            for (int guard = 0; guard < 16; ++guard)
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
                    DebugDeliverTale(context);
                    return _quest != null;

                case ViviEndingStage.Tales:
                    while (!_tales.AllDelivered)
                    {
                        var delivered = _tales.Delivered;
                        DebugDeliverTale(context);
                        if (_tales.Delivered == delivered) { return false; }
                    }

                    _ruins.CompleteTraditionInquiry();
                    return Stage != ViviEndingStage.Tales;

                case ViviEndingStage.Resonance:
                    return _ruins.RevealRuins(ViviEndingUtility.ResonatorOf(ResolveDebugEverflower(context)));

                case ViviEndingStage.Ruins:
                    _ruins.DebugComplete();
                    return Stage != ViviEndingStage.Ruins;

                case ViviEndingStage.AltarUnlocked:
                    var everflower = ResolveDebugEverflower(context);
                    if (everflower == null)
                    {
                        Log.Warning("[ViViRace] ending debug: no everflower to start the siege with.");
                        return false;
                    }

                    return StartBloom(everflower, DebugRitualQuality);

                case ViviEndingStage.Siege:
                    BloomPart?.DebugSetProgress(QuestPart_ViviBloomCondensation.LockedProgress);
                    return Stage != ViviEndingStage.Siege;

                case ViviEndingStage.Calm:
                    return DebugStepCalm(target);

                case ViviEndingStage.Boss:
                    ApocryphaPart?.DebugDefeat();
                    return Stage != ViviEndingStage.Boss;

                case ViviEndingStage.Coronation:
                    var resonator = ViviEndingUtility.ResonatorOf(_anchor);
                    ViviEndingSignals.Send(_quest, ViviEndingSignals.CoronationCompleted, resonator.Named("PAWN"));
                    return Stage != ViviEndingStage.Coronation;

                default:
                    return false;
            }
        }

        private bool DebugStepCalm(ViviEndingStage target)
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

        private static ArcanePlant_Everflower ResolveDebugEverflower(ArcanePlant_Everflower context)
        {
            return context != null && !context.Destroyed ? context : ViviEndingUtility.FindEndingCandidate();
        }

        private void DebugReset()
        {
            var quest = _quest;
            if (quest != null)
            {
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
            ViviEndingUtility.RunStep("debug reset: destroy ruins", _ruins.DebugDestroySite);

            _quest = null;
            _anchor = null;
            _bloomStarted = false;
            _tales = new ViviEndingTales(this);
            _ruins = new ViviAncientRuins(this);
            _epilogue = new ViviEndingEpilogue(this);
            InvalidatePartCache();
        }

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
    }
}
