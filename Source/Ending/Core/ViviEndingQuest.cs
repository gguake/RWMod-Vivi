using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    public class ViviEndingQuest : IExposable
    {
        public static ViviEndingQuest Current => GameComponent_Mana.Of(Verse.Current.Game)?.EndingQuest;

        private const int ScanIntervalTicks = 2500;

        public const int MissiveMinEchoStage = 4;
        private static readonly IntRange MissiveDelayTicks = new IntRange(60000, 120000);

        private Quest _missiveQuest;
        private Quest _bloomQuest;

        private int _missiveDueTick = -1;

        private Pawn _assimilated;

        public ViviEndingQuest()
        {
            _amberCoreRequest = new ViviAmberCoreRequest(this);
            _ambience = new ViviEndingAmbience(this);

            Hediff_EverflowerAssimilation.ResetRegistry();
        }

        public Pawn AssimilatedPawn => _assimilated != null && Hediff_EverflowerAssimilation.IsAssimilated(_assimilated) ? _assimilated : null;

        // 바닐라는 종료된 퀘스트의 부품을 정리 후 30일이 지나면 삭제한다.
        // 축복·제단·날씨 의식에 필요한 앵커는 엔딩 후에도 유지되도록 퀘스트 부품과 별도로 보관한다.
        private ArcanePlant_Everflower _anchor;

        private ViviEndingEchoLetters _echoLetters = new ViviEndingEchoLetters();
        private ViviAmberCoreRequest _amberCoreRequest;
        private ViviEndingAmbience _ambience;

        private Quest _cachedQuest;
        private int _cachedPartCount = -1;
        private QuestPart_ViviEndingState _statePart;
        private QuestPart_ViviBloomCondensation _bloomPart;
        private QuestPart_Delay _calmPart;
        private QuestPart_ViviApocrypha _apocryphaPart;

        public Quest MissiveQuest => _missiveQuest;
        public Quest BloomQuest => _bloomQuest;

        public ViviEndingEchoLetters EchoLetters => _echoLetters;
        public ViviAmberCoreRequest AmberCoreRequest => _amberCoreRequest;
        public ViviEndingAmbience Ambience => _ambience;

        public ViviEndingStage Stage
        {
            get
            {
                if (_bloomQuest != null)
                {
                    switch (_bloomQuest.State)
                    {
                        case QuestState.EndedSuccess:
                            return ViviEndingStage.Completed;

                        case QuestState.Ongoing:
                        case QuestState.NotYetAccepted:
                            return StatePart?.Stage ?? ViviEndingStage.Siege;

                        default:
                            return ViviEndingStage.Failed;
                    }
                }

                return _missiveQuest != null ? ViviEndingStage.AltarUnlocked : ViviEndingStage.None;
            }
        }

        public float BloomProgress
        {
            get
            {
                switch (Stage)
                {
                    case ViviEndingStage.Siege:
                    case ViviEndingStage.Failed:
                        return BloomPart?.Progress ?? 0f;

                    case ViviEndingStage.Calm:
                    case ViviEndingStage.Boss:
                        return QuestPart_ViviBloomCondensation.LockedProgress;

                    case ViviEndingStage.Coronation:
                    case ViviEndingStage.Completed:
                        return 1f;

                    default:
                        return 0f;
                }
            }
        }

        public ArcanePlant_Everflower AnchorEverflower => _anchor;
        public Map AnchorMap => _anchor?.MapHeld;

        public Pawn Boss => _bloomQuest != null && _bloomQuest.State == QuestState.Ongoing ? ApocryphaPart?.Boss : null;
        public CompApocrypha BossComp => Boss?.GetComp<CompApocrypha>();

        public int SiegeDurationTicks => BloomPart?.DurationTicks ?? 0;

        public bool IsAltarUnlocked => Stage != ViviEndingStage.None;
        public bool IsEndingActive => Stage.IsActiveStage();
        public bool IsCompleted => Stage == ViviEndingStage.Completed;

        public bool IsAwaitingCoronation => Stage == ViviEndingStage.Coronation;

        public bool IsBossPhase2 => Stage == ViviEndingStage.Boss && BossComp != null && BossComp.Phase == ApocryphaPhase.Unbound;

        public bool IsAnchor(ArcanePlant_Everflower everflower) => everflower != null && everflower == _anchor;

        public bool ShouldSuppressThreats(Map map)
        {
            if (map == null || map != AnchorMap || IsBossDeferred) { return false; }

            var stage = Stage;
            return stage == ViviEndingStage.Calm || stage == ViviEndingStage.Boss;
        }

        public bool IsRaidBeaconActive(Map map)
        {
            if (map == null || map != AnchorMap) { return false; }

            var stage = Stage;
            return stage == ViviEndingStage.Siege || stage == ViviEndingStage.Calm || stage == ViviEndingStage.Boss;
        }

        internal bool IsBossDeferred => ApocryphaPart != null && ApocryphaPart.State == QuestPartState.Enabled && ApocryphaPart.IsDeferred;

        internal Building_EverflowerAltar AnchorAltar => ViviEndingUtility.AltarOf(_anchor);

        private QuestPart_ViviEndingState StatePart
        {
            get
            {
                RefreshPartCache();
                return _statePart;
            }
        }

        private QuestPart_ViviBloomCondensation BloomPart
        {
            get
            {
                RefreshPartCache();
                return _bloomPart;
            }
        }

        private QuestPart_Delay CalmPart
        {
            get
            {
                RefreshPartCache();
                return _calmPart;
            }
        }

        private QuestPart_ViviApocrypha ApocryphaPart
        {
            get
            {
                RefreshPartCache();
                return _apocryphaPart;
            }
        }

        private void RefreshPartCache()
        {
            var quest = _bloomQuest;
            var partCount = quest?.PartsListForReading.Count ?? -1;
            if (quest == _cachedQuest && partCount == _cachedPartCount) { return; }

            _cachedQuest = quest;
            _cachedPartCount = partCount;
            _statePart = quest?.GetFirstPartOfType<QuestPart_ViviEndingState>();
            _bloomPart = quest?.GetFirstPartOfType<QuestPart_ViviBloomCondensation>();
            _apocryphaPart = quest?.GetFirstPartOfType<QuestPart_ViviApocrypha>();
            _calmPart = quest?.PartsListForReading.OfType<QuestPart_Delay>().FirstOrDefault(v => v.debugLabel == QuestNode_Root_ViviBloom.CalmDelayLabel);
        }

        public void Tick()
        {
            var now = Find.TickManager.TicksGame;

            if (_assimilated != null)
            {
                try
                {
                    Hediff_EverflowerAssimilation.Of(_assimilated)?.TickHeld(now);
                }
                catch (Exception ex)
                {
                    Log.ErrorOnce($"[ViViRace] everflower assimilation tick failed: {ex}", ViviEndingUtility.ErrorKeyAssimilationTick);
                }
            }

            try
            {
                _ambience.Tick(now);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] altar everflower pulse failed: {ex}", ViviEndingUtility.ErrorKeyAltarPulse);
            }

            if (now % ScanIntervalTicks != 0) { return; }

            try
            {
                var stage = Stage;
                if (stage == ViviEndingStage.None)
                {
                    TickMissiveArrival(now);
                }
                else if (stage == ViviEndingStage.AltarUnlocked)
                {
                    ReissueEndedMissiveQuest();
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] ending quest tick failed at stage {Stage}: {ex}", ViviEndingUtility.ErrorKeyQuestTick);
            }
        }

        public void Update()
        {
            _ambience.Update();
        }

        public bool Notify_GrandAttunementCompleted(ArcanePlant_Everflower everflower, float quality)
        {
            if (everflower == null || everflower.Destroyed) { return false; }

            var stage = Stage;
            if (stage != ViviEndingStage.AltarUnlocked)
            {
                Log.Warning($"[ViViRace] full bloom ritual completed at ending stage {stage}; the siege only starts from {ViviEndingStage.AltarUnlocked}.");
                return false;
            }

            return StartBloomQuest(everflower, quality);
        }

        // 소멸 연출 도중 저장·불러오기를 거치면 이미 파괴된 boss 참조는 null일 수 있다.
        public void Notify_BossDefeated()
        {
            if (Stage != ViviEndingStage.Boss) { return; }

            ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.ApocryphaDissolved);
        }

        public void Notify_CoronationCompleted(ArcanePlant_Everflower everflower, Pawn queen)
        {
            if (Stage != ViviEndingStage.Coronation || !IsAnchor(everflower)) { return; }

            ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.CoronationCompleted, queen.Named("PAWN"));
        }

        public void Notify_EverflowerStolen()
        {
            if (!IsEndingActive) { return; }

            ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.EverflowerStolen);
        }

        // 엔딩 진행 중 앵커의 제단을 잃으면 복구하지 않고 실패로 끝낸다.
        public void Notify_AnchorAltarLost(ArcanePlant_Everflower everflower)
        {
            if (!IsEndingActive || !IsAnchor(everflower)) { return; }

            ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.AnchorLost);
        }

        public void Notify_EverflowerRitualEcho(ArcanePlant_Everflower everflower, Pawn resonator)
        {
            _echoLetters.Notify_EverflowerRitualEcho(everflower, resonator);
        }

        public IEnumerable<Gizmo> GetEverflowerGizmos(ArcanePlant_Everflower everflower)
        {
            if (everflower == null) { yield break; }

            if (IsAnchor(everflower) && IsEndingActive)
            {
                yield return new Gizmo_ViviEndingBloom(this);
            }

            foreach (var gizmo in GetDebugGizmos(everflower))
            {
                yield return gizmo;
            }
        }

        public string GetEverflowerInspectString(ArcanePlant_Everflower everflower)
        {
            if (!IsAnchor(everflower) || !IsEndingActive) { return null; }

            return LocalizeString_Inspector.VV_Inspector_EndingBloom.Translate(BloomProgress.ToStringPercent().Named("PERCENT")).Resolve();
        }

        public static bool MissiveArrivalConditionsMet()
        {
            var quest = Current;
            if (quest == null || quest.Stage != ViviEndingStage.None) { return false; }
            if (quest.EchoLetters.LastStage < MissiveMinEchoStage) { return false; }

            var everflower = ViviEndingUtility.FindEndingCandidate();
            if (everflower == null || everflower.AttunementLevel < Building_EverflowerAltar.RequiredAttunementLevel) { return false; }
            if (!Building_EverflowerAltar.IsValidAltarMap(everflower.MapHeld)) { return false; }

            return ViviEndingUtility.FindFederation(requireNonHostile: true) != null;
        }

        private void TickMissiveArrival(int now)
        {
            if (!MissiveArrivalConditionsMet()) { return; }

            if (_missiveDueTick < 0)
            {
                _missiveDueTick = now + MissiveDelayTicks.RandomInRange;
                return;
            }

            if (now >= _missiveDueTick)
            {
                StartMissiveQuest(null);
            }
        }

        public void BeginAssimilation(Pawn resonator, ArcanePlant_Everflower everflower)
        {
            if (resonator == null || everflower == null || Stage != ViviEndingStage.AltarUnlocked) { return; }

            var altar = everflower.EverflowerComp?.EnshrinedAltar;
            if (altar == null) { return; }

            if (Hediff_EverflowerAssimilation.Begin(resonator, everflower, altar) != null)
            {
                _assimilated = resonator;
            }
        }

        public bool ShouldKeepAssimilation(Pawn pawn, ArcanePlant_Everflower everflower)
        {
            if (pawn == null || pawn != _assimilated) { return false; }

            switch (Stage)
            {
                case ViviEndingStage.AltarUnlocked:
                    return pawn.GetLord()?.LordJob is LordJob_Ritual ritual
                        && ritual.Ritual?.def == VVPreceptDefOf.VV_GrandAttunement
                        && ritual.selectedTarget.Thing == everflower;

                case ViviEndingStage.Siege:
                case ViviEndingStage.Calm:
                case ViviEndingStage.Boss:
                    return IsAnchor(everflower);

                default:
                    return false;
            }
        }

        public void Notify_AssimilationEnded(Pawn pawn)
        {
            if (pawn != null && pawn == _assimilated)
            {
                _assimilated = null;
            }
        }

        public void Notify_AssimilationBroken(Pawn pawn)
        {
            if (pawn == null || pawn != _assimilated) { return; }

            var stage = Stage;
            if (stage == ViviEndingStage.Siege || stage == ViviEndingStage.Calm || stage == ViviEndingStage.Boss)
            {
                ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.ResonatorFallen, pawn.Named("PAWN"));
                return;
            }

            if (stage != ViviEndingStage.AltarUnlocked) { return; }

            Messages.Message(
                LocalizeString_Message.VV_Message_AssimilationBrokenRitual.Translate(pawn.Named("PAWN")),
                new LookTargets(pawn.PositionHeld, pawn.MapHeld),
                MessageTypeDefOf.NegativeEvent);
        }

        public bool StartMissiveQuest(ArcanePlant_Everflower everflower)
        {
            if (_missiveQuest != null || _bloomQuest != null) { return false; }

            return CreateMissiveQuest(everflower, reissued: false);
        }

        private bool CreateMissiveQuest(ArcanePlant_Everflower everflower, bool reissued)
        {
            if (everflower == null || everflower.Destroyed)
            {
                everflower = ViviEndingUtility.FindEndingCandidate();
            }

            var slate = new Slate();
            if (everflower != null)
            {
                slate.Set("everflower", everflower);
            }
            slate.Set("reissued", reissued);
            SetPoints(slate, everflower?.MapHeld ?? Find.AnyPlayerHomeMap);

            var quest = GenerateQuest(VVQuestScriptDefOf.VV_EndGame_ViviMissive, slate, ViviEndingSignals.GrandAttunementCompleted);
            if (quest == null) { return false; }

            _missiveQuest = quest;
            Find.QuestManager.Add(quest);
            return true;
        }

        // 만개 의식 외의 이유로 밀서 퀘스트가 종료되면 서신을 재발송하지 않고 퀘스트만 다시 만든다.
        // 다른 모드나 개발자 도구의 종료로 코어 의뢰와 만개 의식이 막히는 일을 방지한다.
        private void ReissueEndedMissiveQuest()
        {
            var ended = _missiveQuest;
            if (ended == null || !ended.Historical) { return; }

            if (!CreateMissiveQuest(null, reissued: true)) { return; }

            Log.Warning($"[ViViRace] the federation missive quest ended unexpectedly ({ended.State}); issued it again.");
        }

        private bool StartBloomQuest(ArcanePlant_Everflower everflower, float quality)
        {
            var map = everflower.MapHeld;
            var slate = new Slate();
            slate.Set("anchor", everflower);
            if (map != null)
            {
                slate.Set("map", map);
            }
            slate.Set("quality", quality);
            if (_assimilated != null)
            {
                slate.Set("resonator", _assimilated);
            }
            SetPoints(slate, map);

            var quest = GenerateQuest(VVQuestScriptDefOf.VV_EndGame_ViviBloom, slate, ViviEndingSignals.AnchorLost);
            if (quest == null) { return false; }

            // 퀘스트를 추가하는 즉시 시작 신호가 처리되므로 상태를 먼저 갱신한다.
            _bloomQuest = quest;
            _anchor = everflower;
            Find.QuestManager.Add(quest);

            if (_missiveQuest != null && _missiveQuest.State == QuestState.Ongoing)
            {
                ViviEndingSignals.Send(_missiveQuest, ViviEndingSignals.GrandAttunementCompleted);
            }

            return true;
        }

        private static void SetPoints(Slate slate, Map map)
        {
            slate.Set("points", map != null ? StorytellerUtility.DefaultThreatPointsNow(map) : 0f);
        }

        // QuestNode.Run은 예외가 나도 오류만 기록하고 불완전한 퀘스트를 반환한다.
        // 루트가 마지막에 추가하는 finalSignal 수신 QuestEnd까지 있어야 생성이 완료된 것으로 판단한다.
        private static Quest GenerateQuest(QuestScriptDef def, Slate slate, string finalSignal)
        {
            if (def == null)
            {
                Log.Error("[ViViRace] ending quest script def is missing.");
                return null;
            }

            var quest = QuestGen.Generate(def, slate);
            if (quest == null)
            {
                Log.Error($"[ViViRace] failed to generate ending quest {def.defName}.");
                return null;
            }

            var endSignal = ViviEndingSignals.For(quest, finalSignal);
            if (!quest.PartsListForReading.Any(v => v is QuestPart_QuestEnd end && end.inSignal == endSignal))
            {
                Log.Error($"[ViViRace] ending quest {def.defName} was generated without all of its parts; discarded it.");
                return null;
            }

            return quest;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref _missiveQuest, "missiveQuest");
            Scribe_References.Look(ref _bloomQuest, "bloomQuest");
            Scribe_References.Look(ref _anchor, "anchorEverflower");
            Scribe_Values.Look(ref _missiveDueTick, "missiveDueTick", -1);
            Scribe_References.Look(ref _assimilated, "assimilatedPawn");
            Scribe_Deep.Look(ref _echoLetters, "echoLetters");
            Scribe_Deep.Look(ref _amberCoreRequest, "amberCoreRequest", this);
        }

        #region 개발자 도구
        private const float DebugRitualQuality = 5f;

        private bool IsFinished => Stage is ViviEndingStage.Completed or ViviEndingStage.Failed;

        private IEnumerable<Gizmo> GetDebugGizmos(ArcanePlant_Everflower everflower)
        {
            if (!DebugSettings.godMode || everflower.Faction != Faction.OfPlayer) { yield break; }

            if (_anchor != null && !_anchor.Destroyed && !IsAnchor(everflower)) { yield break; }

            var stage = Stage;
            var finished = IsFinished;

            if (!IsAltarUnlocked)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Start missive quest now",
                    action = () => StartMissiveQuest(everflower),
                };
            }

            if (_echoLetters.LastStage < ViviEndingEchoLetters.MaxStage)
            {
                yield return new Command_Action()
                {
                    defaultLabel = $"DEV: Send echo letter {_echoLetters.LastStage + 1}",
                    action = () => _echoLetters.DebugSendNext(everflower),
                };
            }

            if (_echoLetters.LastStage > 0)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Reset echo letters",
                    action = () => _echoLetters.DebugSetLastStage(0),
                };
            }

            if (IsAltarUnlocked && !finished)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Request amber core quest now",
                    action = DebugRequestAmberCoreQuest,
                };
            }

            if (!finished)
            {
                // 열거형 값 1이 비어 있으므로 None 다음은 AltarUnlocked로 직접 지정한다.
                var next = stage == ViviEndingStage.None ? ViviEndingStage.AltarUnlocked : stage + 1;
                yield return new Command_Action()
                {
                    defaultLabel = $"DEV: Ending stage → {next}",
                    action = () => DebugSetStage(next, everflower),
                };
            }

            if (stage == ViviEndingStage.Siege)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Bloom +10%",
                    action = () => DebugSetBloom(BloomProgress + 0.1f),
                };
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Bloom → 99%",
                    action = () => DebugSetBloom(QuestPart_ViviBloomCondensation.LockedProgress),
                };
            }

            if (stage == ViviEndingStage.Calm)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: End calm now",
                    action = DebugEndCalm,
                };
            }

            if (!finished && stage != ViviEndingStage.Coronation && Boss == null)
            {
                var manifesting = ApocryphaPart != null && ApocryphaPart.IsManifesting;
                yield return new Command_Action()
                {
                    defaultLabel = manifesting ? "DEV: Skip Asphodel intro" : "DEV: Spawn Asphodel now",
                    action = () => DebugSpawnBossNow(everflower),
                };
            }

            if (!finished)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Complete ending",
                    action = () => DebugSetStage(ViviEndingStage.Completed, everflower),
                };
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Fail ending",
                    action = () => DebugSetStage(ViviEndingStage.Failed, everflower),
                };
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

        private void DebugRequestAmberCoreQuest()
        {
            if (_amberCoreRequest.Request(ViviEndingUtility.FindFederation(requireNonHostile: false)) == null)
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
        #endregion
    }
}
