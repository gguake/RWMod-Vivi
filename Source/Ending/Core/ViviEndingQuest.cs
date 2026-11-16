using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 동화책 1권부터 대관식까지 에픽 퀘스트 하나로 진행한다. 개화 전 단계(동화책·밀서·유적)의 상태는 퀘스트 부품이 아니라 보조 객체가 가진다.
    public class ViviEndingQuest : IExposable
    {
        public static ViviEndingQuest Current => GameComponent_Mana.Of(Verse.Current.Game)?.EndingQuest;

        private const int ScanIntervalTicks = 2500;
        private const int CoreLostCacheTicks = 60;

        private Quest _quest;
        private ArcanePlant_Everflower _anchor;
        private Pawn _assimilated;

        // 끝난 퀘스트는 30일 뒤 부품이 지워지므로 개화 시작 여부는 따로 저장한다.
        private bool _bloomStarted;

        private ViviEndingTales _tales;
        private ViviAncientRuins _ruins;
        private ViviEndingEpilogue _epilogue;
        private ViviEndingAmbience _ambience;

        private Quest _cachedQuest;
        private int _cachedPartCount = -1;
        private QuestPart_ViviEndingState _statePart;
        private QuestPart_ViviBloomCondensation _bloomPart;
        private QuestPart_Delay _calmPart;
        private QuestPart_ViviApocrypha _apocryphaPart;

        private int _coreLostCheckTick = -1;
        private bool _coreLost;

        public ViviEndingQuest()
        {
            _tales = new ViviEndingTales(this);
            _ruins = new ViviAncientRuins(this);
            _epilogue = new ViviEndingEpilogue(this);
            _ambience = new ViviEndingAmbience(this);

            Hediff_EverflowerAssimilation.ResetRegistry();
        }

        public Quest Quest => _quest;

        public ViviEndingTales Tales => _tales;
        public ViviAncientRuins Ruins => _ruins;
        public ViviEndingEpilogue Epilogue => _epilogue;
        public ViviEndingAmbience Ambience => _ambience;

        public Pawn AssimilatedPawn => _assimilated != null && Hediff_EverflowerAssimilation.IsAssimilated(_assimilated) ? _assimilated : null;

        public ViviEndingStage Stage
        {
            get
            {
                if (_quest == null) { return ViviEndingStage.None; }

                var state = StatePart;
                switch (_quest.State)
                {
                    case QuestState.EndedSuccess:
                        return ViviEndingStage.Completed;

                    case QuestState.Ongoing:
                    case QuestState.NotYetAccepted:
                        return state?.Stage ?? JourneyStage;

                    default:
                        return _bloomStarted ? ViviEndingStage.Failed : JourneyStage;
                }
            }
        }

        private ViviEndingStage JourneyStage
        {
            get
            {
                if (_ruins.CoreExtracted) { return ViviEndingStage.AltarUnlocked; }
                if (_ruins.HasCoordinates) { return ViviEndingStage.Ruins; }
                if (_ruins.MissiveReceived) { return ViviEndingStage.Missive; }
                return ViviEndingStage.Tales;
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

        public Pawn Boss => _quest != null && _quest.State == QuestState.Ongoing ? ApocryphaPart?.Boss : null;
        public CompApocrypha BossComp => Boss?.GetComp<CompApocrypha>();

        public int SiegeDurationTicks => BloomPart?.DurationTicks ?? 0;

        public bool IsAltarUnlocked => Stage >= ViviEndingStage.AltarUnlocked;
        public bool IsEndingActive => Stage.IsActiveStage();
        public bool IsCompleted => Stage == ViviEndingStage.Completed;

        public bool IsAwaitingCoronation => Stage == ViviEndingStage.Coronation;

        // 제단 단계에서 코어와 제단이 어디에도 없으면 분실로 본다. 설명란이 매 프레임 읽으므로 잠시 캐시한다.
        public bool IsCoreLost
        {
            get
            {
                if (Stage != ViviEndingStage.AltarUnlocked) { return false; }

                var now = Find.TickManager.TicksGame;
                if (_coreLostCheckTick < 0 || now < _coreLostCheckTick || now - _coreLostCheckTick >= CoreLostCacheTicks)
                {
                    _coreLostCheckTick = now;
                    _coreLost = !AmberCoreUtility.AnyAmberCoreExists();
                }

                return _coreLost;
            }
        }

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
            var quest = _quest;
            var partCount = quest?.PartsListForReading.Count ?? -1;
            if (quest == _cachedQuest && partCount == _cachedPartCount) { return; }

            _cachedQuest = quest;
            _cachedPartCount = partCount;
            _statePart = quest?.GetFirstPartOfType<QuestPart_ViviEndingState>();
            _bloomPart = quest?.GetFirstPartOfType<QuestPart_ViviBloomCondensation>();
            _apocryphaPart = quest?.GetFirstPartOfType<QuestPart_ViviApocrypha>();
            _calmPart = quest?.PartsListForReading.OfType<QuestPart_Delay>().FirstOrDefault(v => v.debugLabel == ViviEndingBloomParts.CalmDelayLabel);
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
                ReissueEndedQuest();

                switch (Stage)
                {
                    case ViviEndingStage.Tales:
                        _ruins.TickMissive(now);
                        break;

                    case ViviEndingStage.Ruins:
                        _ruins.TickSite();
                        break;

                    case ViviEndingStage.Completed:
                        _epilogue.Tick(now);
                        break;
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

        // 〈영원꽃 조율 의식〉을 마칠 때마다 연방이 비적대이면 아직 받지 못한 다음 권을 한 권 보낸다.
        public void Notify_AttunementRitualCompleted(ArcanePlant_Everflower everflower)
        {
            if (everflower == null || everflower.Destroyed || everflower.Faction != Faction.OfPlayer) { return; }

            var stage = Stage;
            if (stage != ViviEndingStage.None && stage != ViviEndingStage.Tales) { return; }
            if (!_tales.CanDeliverNext(everflower)) { return; }

            var federation = ViviEndingUtility.FindFederation(requireNonHostile: true);
            if (federation == null) { return; }

            if (_quest == null && !CreateQuest()) { return; }

            _tales.DeliverNext(everflower, federation);
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

            return StartBloom(everflower, quality);
        }

        public void Notify_BossDefeated()
        {
            if (Stage != ViviEndingStage.Boss) { return; }

            ViviEndingSignals.Send(_quest, ViviEndingSignals.ApocryphaDissolved);
        }

        public void Notify_CoronationCompleted(ArcanePlant_Everflower everflower, Pawn queen)
        {
            if (Stage != ViviEndingStage.Coronation || !IsAnchor(everflower)) { return; }

            ViviEndingSignals.Send(_quest, ViviEndingSignals.CoronationCompleted, queen.Named("PAWN"));
        }

        public void Notify_EverflowerStolen()
        {
            if (!IsEndingActive) { return; }

            ViviEndingSignals.Send(_quest, ViviEndingSignals.EverflowerStolen);
        }

        public void Notify_AnchorAltarLost(ArcanePlant_Everflower everflower)
        {
            if (!IsEndingActive || !IsAnchor(everflower)) { return; }

            ViviEndingSignals.Send(_quest, ViviEndingSignals.AnchorLost);
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
                ViviEndingSignals.Send(_quest, ViviEndingSignals.ResonatorFallen, pawn.Named("PAWN"));
                return;
            }

            if (stage != ViviEndingStage.AltarUnlocked) { return; }

            Messages.Message(
                LocalizeString_Message.VV_Message_AssimilationBrokenRitual.Translate(pawn.Named("PAWN")),
                new LookTargets(pawn.PositionHeld, pawn.MapHeld),
                MessageTypeDefOf.NegativeEvent);
        }

        private bool CreateQuest()
        {
            var slate = new Slate();
            var map = Find.AnyPlayerHomeMap;
            slate.Set("points", map != null ? StorytellerUtility.DefaultThreatPointsNow(map) : 0f);

            var quest = GenerateQuest(VVQuestScriptDefOf.VV_EndGame_ViviEnding, slate, ViviEndingSignals.EndingFinished);
            if (quest == null) { return false; }

            _quest = quest;
            Find.QuestManager.Add(quest);
            return true;
        }

        // 개화 전 단계에서 퀘스트가 뜻밖에 끝나면 진행 상태는 보조 객체에 남아 있으므로 같은 단계로 다시 띄운다.
        private void ReissueEndedQuest()
        {
            var ended = _quest;
            if (ended == null || !ended.Historical || _bloomStarted) { return; }

            _quest = null;
            if (!CreateQuest())
            {
                _quest = ended;
                return;
            }

            Log.Warning($"[ViViRace] the ending quest ended unexpectedly before the bloom ({ended.State}); issued it again.");
        }

        private bool StartBloom(ArcanePlant_Everflower everflower, float quality)
        {
            if (_quest == null || _quest.State != QuestState.Ongoing || _bloomStarted || StatePart != null) { return false; }

            var added = ViviEndingBloomParts.TryAdd(_quest, everflower, quality, _assimilated);
            _cachedQuest = null;
            _cachedPartCount = -1;
            if (!added) { return false; }

            _bloomStarted = true;
            _anchor = everflower;
            ViviEndingSignals.Send(_quest, ViviEndingSignals.BloomStarted);
            return true;
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
            Scribe_References.Look(ref _quest, "quest");
            Scribe_References.Look(ref _anchor, "anchorEverflower");
            Scribe_References.Look(ref _assimilated, "assimilatedPawn");
            Scribe_Values.Look(ref _bloomStarted, "bloomStarted", false);
            Scribe_Deep.Look(ref _tales, "tales", this);
            Scribe_Deep.Look(ref _ruins, "ruins", this);
            Scribe_Deep.Look(ref _epilogue, "epilogue", this);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_tales == null) { _tales = new ViviEndingTales(this); }
                if (_ruins == null) { _ruins = new ViviAncientRuins(this); }
                if (_epilogue == null) { _epilogue = new ViviEndingEpilogue(this); }
            }
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

            if ((stage == ViviEndingStage.None || stage == ViviEndingStage.Tales) && !_tales.AllDelivered)
            {
                yield return new Command_Action()
                {
                    defaultLabel = $"DEV: Deliver fairy tale {_tales.Delivered + 1}",
                    action = () => DebugDeliverTale(everflower),
                };
            }

            if (stage < ViviEndingStage.AltarUnlocked)
            {
                yield return new Command_Action()
                {
                    defaultLabel = "DEV: Unlock altar now",
                    action = DebugUnlockAltar,
                };
            }

            if (stage == ViviEndingStage.Ruins && _ruins.Investigated < ViviAncientRuins.EchoCount)
            {
                yield return new Command_Action()
                {
                    defaultLabel = $"DEV: Ruin echo {_ruins.Investigated + 1}",
                    action = () => _ruins.DebugInvestigateNext(ViviEndingUtility.ResonatorOf(everflower)),
                };
            }

            if (!finished)
            {
                var next = stage + 1;
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

            if (!finished && stage >= ViviEndingStage.AltarUnlocked && stage != ViviEndingStage.Coronation && Boss == null)
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

        // 편지·드롭 없이 동화책·밀서·유적을 마친 상태로 만든다(시험 장면용).
        public void DebugUnlockAltar()
        {
            if (Stage >= ViviEndingStage.AltarUnlocked) { return; }
            if (_quest == null && !CreateQuest()) { return; }

            _tales.DebugSetDelivered(ViviEndingTales.VolumeCount);
            _ruins.DebugComplete();
        }

        public void DebugDeliverTale(ArcanePlant_Everflower context)
        {
            if (_tales.AllDelivered) { return; }

            if (_quest == null && !CreateQuest()) { return; }

            var everflower = context != null && !context.Destroyed ? context : ViviEndingUtility.FindEndingCandidate();
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

                    _ruins.SendMissive(ViviEndingUtility.FindFederation(requireNonHostile: false));
                    return Stage != ViviEndingStage.Tales;

                case ViviEndingStage.Missive:
                    return _ruins.RevealRuins();

                case ViviEndingStage.Ruins:
                    _ruins.DebugComplete();
                    return Stage != ViviEndingStage.Ruins;

                case ViviEndingStage.AltarUnlocked:
                    {
                        var everflower = context != null && !context.Destroyed ? context : ViviEndingUtility.FindEndingCandidate();
                        if (everflower == null)
                        {
                            Log.Warning("[ViViRace] ending debug: no everflower to start the siege with.");
                            return false;
                        }

                        return StartBloom(everflower, DebugRitualQuality);
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
                    ViviEndingSignals.Send(_quest, ViviEndingSignals.CoronationCompleted, ViviEndingUtility.ResonatorOf(_anchor).Named("PAWN"));
                    return Stage != ViviEndingStage.Coronation;

                default:
                    return false;
            }
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
            _cachedQuest = null;
            _cachedPartCount = -1;
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
        #endregion
    }
}
