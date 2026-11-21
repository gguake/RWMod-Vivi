using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 동화책 1권부터 대관식까지 에픽 퀘스트 하나로 진행한다. 개화 전 단계(동화책·밀서·유적)의 상태는 퀘스트 부품이 아니라 보조 객체가 가진다.
    public partial class ViviEndingQuest : IExposable
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
                        return _bloomStarted ? (state?.Stage ?? JourneyStage) : JourneyStage;

                    default:
                        return JourneyStage;
                }
            }
        }

        private ViviEndingStage JourneyStage
        {
            get
            {
                if (_ruins.CoreExtracted) { return ViviEndingStage.AltarUnlocked; }
                if (_ruins.HasCoordinates) { return ViviEndingStage.Ruins; }
                if (_ruins.TraditionInquired) { return ViviEndingStage.Resonance; }
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

        public Pawn Boss => _bloomStarted && _quest != null && _quest.State == QuestState.Ongoing ? ApocryphaPart?.Boss : null;
        public CompApocrypha BossComp => Boss?.GetComp<CompApocrypha>();

        public int SiegeDurationTicks => _bloomStarted ? (BloomPart?.DurationTicks ?? 0) : 0;

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
            return map != null && map == AnchorMap && Stage.IsPreCoronationStage();
        }

        internal bool IsBossDeferred => _bloomStarted && ApocryphaPart != null && ApocryphaPart.State == QuestPartState.Enabled && ApocryphaPart.IsDeferred;

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

        private void InvalidatePartCache()
        {
            _cachedQuest = null;
            _cachedPartCount = -1;
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
            _calmPart = ViviEndingUtility.FindCalmDelayPart(quest);
        }

        public void Tick()
        {
            var now = Find.TickManager.TicksGame;

            if (!_bloomStarted && _quest != null && StatePart != null)
            {
                RemoveStaleBloomParts();
            }

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

        // 관례 문의 이후 영원꽃 의식을 마칠 때마다 숨겨진 마나의 파장(고대 유적) 포착을 판정한다.
        public bool Notify_EverflowerRitualCompleted(ArcanePlant_Everflower everflower, Pawn resonator)
        {
            if (everflower == null || everflower.Destroyed || everflower.Faction != Faction.OfPlayer) { return false; }
            if (Stage != ViviEndingStage.Resonance) { return false; }

            return _ruins.TryDetectOnRitual(everflower, resonator);
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

        // 개화가 중단되면 부수 효과를 즉시 정리하고 만개 의식 대기(AltarUnlocked)로 되돌린다.
        // 퀘스트 부품 제거는 신호 전파 중 컬렉션 변경을 피하기 위해 다음 틱(또는 재시작 직전)에 수행한다.
        internal void Notify_BloomFailed(ArcanePlant_Everflower anchor, MapParent anchorMapParent, bool stolen)
        {
            ViviEndingBloomParts.CleanupAndDetachTags(_quest, anchor, anchorMapParent);

            if (!stolen && anchor != null && !anchor.Destroyed
                && anchor.AttunementLevel == RitualOutcomeEffectWorker_GrandAttunement.GrandAttunementLevel)
            {
                anchor.EverflowerComp?.SetAttunementLevel(Building_EverflowerAltar.RequiredAttunementLevel, sendLetter: false);
            }

            _anchor = null;
            _bloomStarted = false;

            if (_assimilated != null)
            {
                Hediff_EverflowerAssimilation.Of(_assimilated)?.Release();
            }
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

            var stage = Stage;
            if (stage == ViviEndingStage.AltarUnlocked)
            {
                return pawn.GetLord()?.LordJob is LordJob_Ritual ritual
                    && ritual.Ritual?.def == VVPreceptDefOf.VV_GrandAttunement
                    && ritual.selectedTarget.Thing == everflower;
            }

            return stage.IsPreCoronationStage() && IsAnchor(everflower);
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
            if (stage.IsPreCoronationStage())
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

        private bool CreateQuest(bool sendLetter = true)
        {
            var slate = new Slate();
            var map = Find.AnyPlayerHomeMap;
            slate.Set("points", map != null ? StorytellerUtility.DefaultThreatPointsNow(map) : 0f);

            var quest = GenerateQuest(VVQuestScriptDefOf.VV_EndGame_ViviEnding, slate, ViviEndingSignals.EndingFinished);
            if (quest == null) { return false; }

            _quest = quest;
            Find.QuestManager.Add(quest);

            if (sendLetter && !quest.hidden && quest.root.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            return true;
        }

        // 완료되지 않은 채 퀘스트가 끝나면(구버전 실패 세이브 포함) 진행 상태는 보조 객체에 남아 있으므로 같은 단계로 다시 띄운다.
        private void ReissueEndedQuest()
        {
            var ended = _quest;
            if (ended == null || !ended.Historical || ended.State == QuestState.EndedSuccess) { return; }

            _quest = null;
            if (!CreateQuest(sendLetter: false))
            {
                _quest = ended;
                return;
            }

            _anchor = null;
            _bloomStarted = false;
            InvalidatePartCache();

            Log.Warning($"[ViViRace] the ending quest ended without completing ({ended.State}); issued it again.");
        }

        private void RemoveStaleBloomParts()
        {
            ViviEndingBloomParts.Remove(_quest);
            InvalidatePartCache();
        }

        private bool StartBloom(ArcanePlant_Everflower everflower, float quality)
        {
            if (_quest == null || _quest.State != QuestState.Ongoing || _bloomStarted) { return false; }

            if (StatePart != null)
            {
                RemoveStaleBloomParts();
            }

            var added = ViviEndingBloomParts.TryAdd(_quest, everflower, quality, _assimilated);
            InvalidatePartCache();
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
    }
}
