using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 개발자 도구: 영원꽃의 DEV 기즈모(갓 모드)와 자동 테스트용 단계 이동. 단계 이동은 퀘스트 신호·부품 완료로만 한다.
    public partial class ViviEndingQuest
    {
        private const float DebugRitualQuality = 5f;

        private IEnumerable<Gizmo> GetDebugGizmos(ArcanePlant_Everflower everflower)
        {
            if (!DebugSettings.godMode || everflower.Faction != Faction.OfPlayer) { yield break; }

            // 엔딩이 영원꽃에 묶인 뒤에는 앵커에만 단계 조작 기즈모를 보여준다.
            if (_anchor != null && !_anchor.Destroyed && !IsAnchor(everflower)) { yield break; }

            var stage = Stage;
            var finished = IsFinished;

            if (!IsAltarUnlocked)
            {
                yield return DevCommand("DEV: Send federation letter now", () => SendFederationLetter(everflower));
            }

            if (IsAltarUnlocked && !finished)
            {
                yield return DevCommand("DEV: Deliver amber core now", () => DebugDeliverAmberCore(everflower.MapHeld));
            }

            if (!finished)
            {
                var next = NextStage(stage);
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
                yield return DevCommand("DEV: Spawn Apocrypha now", () => DebugSpawnBossNow(everflower));
            }

            if (!finished)
            {
                yield return DevCommand("DEV: Complete ending", () => DebugSetStage(ViviEndingStage.Completed, everflower));
                yield return DevCommand("DEV: Fail ending", () => DebugSetStage(ViviEndingStage.Failed, everflower));
            }
        }

        // DEV: 자동 테스트/개발자 기즈모용
        public void DebugSetStage(ViviEndingStage stage)
        {
            DebugSetStage(stage, null);
        }

        // DEV: 공성 중 응축률을 바꾼다(99% 이상이면 곧바로 평화 기간으로).
        public void DebugSetBloom(float value)
        {
            if (Stage != ViviEndingStage.Siege) { return; }

            BloomPart?.DebugSetProgress(value);
        }

        public void DebugSpawnBossNow()
        {
            DebugSpawnBossNow(null);
        }

        // 목표 단계로 옮긴다. context는 앵커가 아직 없을 때 엔딩을 묶을 영원꽃.
        internal void DebugSetStage(ViviEndingStage stage, ArcanePlant_Everflower context)
        {
            switch (stage)
            {
                case ViviEndingStage.None:
                    DebugReset();
                    break;

                case ViviEndingStage.LetterPending:
                    DebugReset();
                    ScheduleFederationLetter();
                    break;

                case ViviEndingStage.Failed:
                    // 본편 퀘스트를 앵커 소실로 실패시킨다(살아 있는 보스·호위는 퀘스트 종료 정리가 치운다).
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

            ApocryphaPart?.DebugSpawnNow();
        }

        // DEV: 평화 기간을 곧바로 끝낸다(아포크리파는 다음 틱에 등장을 시도한다). 이미 보류 중이면 곧바로 다시 시도하게 한다.
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

        private void DebugDeliverAmberCore(Map map)
        {
            var part = _missiveQuest?.GetFirstPartOfType<QuestPart_ViviAmberCore>();
            if (part == null)
            {
                Log.Warning("[ViViRace] ending debug: no missive quest to deliver the amber core with.");
                return;
            }

            part.Deliver(map, redelivery: false);
        }

        // 목표 단계(진행 순서 기준)에 이를 때까지 한 단계씩 넘긴다. 실패 상태에서는 넘기지 않는다.
        private bool DebugAdvanceTo(ViviEndingStage target, ArcanePlant_Everflower context)
        {
            var targetOrder = ProgressOrder(target);
            for (int guard = 0; guard < 10; ++guard)
            {
                var stage = Stage;
                if (stage == ViviEndingStage.Failed) { return false; }
                if (ProgressOrder(stage) >= targetOrder) { return true; }

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

            return ProgressOrder(Stage) >= targetOrder;
        }

        private bool DebugStep(ViviEndingStage stage, ViviEndingStage target, ArcanePlant_Everflower context)
        {
            switch (stage)
            {
                case ViviEndingStage.None:
                case ViviEndingStage.LetterPending:
                    SendFederationLetter(context);
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
                        // 평화 타이머를 끝내 아포크리파 부품을 켠다. 보스 단계가 목표면 등장시키고, 그 너머가 목표면 토벌된 것으로 친다.
                        if (CalmPart != null && CalmPart.State == QuestPartState.Enabled)
                        {
                            CalmPart.DebugForceComplete();
                        }

                        var apocrypha = ApocryphaPart;
                        if (apocrypha == null || apocrypha.State != QuestPartState.Enabled) { return false; }

                        if (target == ViviEndingStage.Boss)
                        {
                            apocrypha.DebugSpawnNow();
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

        // DEV: 엔딩을 처음 상태로 되돌린다. 진행 중인 퀘스트는 결과 없이 끝내(종료 정리로 보스·기믹·컨디션을 걷음) 퀘스트 목록에서 지운다.
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
            }

            ViviEndingUtility.RunStep("debug reset: cleanup gimmicks", ViviEndingUtility.CleanupGimmicks);
            ViviEndingUtility.RunStep("debug reset: end ominous silence", ViviEndingUtility.EndOminousSilence);
            ViviEndingUtility.RunStep("debug reset: end golden bloom", ViviEndingUtility.EndGoldenBloom);

            _missiveQuest = null;
            _bloomQuest = null;
            _anchor = null;
            _letterTick = -1;
            _cachedQuest = null;
            _cachedPartCount = -1;
        }

        // 진행 순서상 위치(단계 값은 저장 호환 때문에 진행 순서와 다르다).
        private static int ProgressOrder(ViviEndingStage stage)
        {
            switch (stage)
            {
                case ViviEndingStage.None: return 0;
                case ViviEndingStage.LetterPending: return 1;
                case ViviEndingStage.AltarUnlocked: return 2;
                case ViviEndingStage.Siege: return 3;
                case ViviEndingStage.Calm: return 4;
                case ViviEndingStage.Boss: return 5;
                case ViviEndingStage.Coronation: return 6;
                case ViviEndingStage.Completed: return 7;
                default: return 8;
            }
        }

        // 진행 순서상 다음 단계(개발자 기즈모용).
        private static ViviEndingStage NextStage(ViviEndingStage stage)
        {
            switch (stage)
            {
                case ViviEndingStage.None: return ViviEndingStage.LetterPending;
                case ViviEndingStage.LetterPending: return ViviEndingStage.AltarUnlocked;
                case ViviEndingStage.AltarUnlocked: return ViviEndingStage.Siege;
                case ViviEndingStage.Siege: return ViviEndingStage.Calm;
                case ViviEndingStage.Calm: return ViviEndingStage.Boss;
                case ViviEndingStage.Boss: return ViviEndingStage.Coronation;
                default: return ViviEndingStage.Completed;
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
