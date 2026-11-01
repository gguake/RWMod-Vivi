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
    public partial class ViviEndingQuest : IExposable
    {
        // 결속 해방 후 귀환을 기다리는 비비. 월드 폰에 넘기지 않고 여기서만 Deep 저장해 귀환 틱에 제단 앞에 스폰한다.
        public class HeldPawn : IExposable
        {
            public Pawn pawn;
            public int returnTick;
            public Map map;

            public void ExposeData()
            {
                // 대기 중 맵이 사라지면 유효하지 않은 참조를 저장하지 않는다. 귀환 시 엔딩 맵이나 다른 정착지로 대체한다.
                if (Scribe.mode == LoadSaveMode.Saving && map != null && !Find.Maps.Contains(map))
                {
                    map = null;
                }

                Scribe_Deep.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref returnTick, "returnTick", 0);
                Scribe_References.Look(ref map, "map");
            }
        }

        public static ViviEndingQuest Current => Verse.Current.Game?.GetComponent<GameComponent_Mana>()?.EndingQuest;

        private const int ScanIntervalTicks = 2500;
        private const float ReturnCellSearchRadius = 8f;
        private const int HeldPawnRetryTicks = 2500;

        public const int MissiveMinEchoStage = 4;
        private static readonly IntRange MissiveDelayTicks = new IntRange(60000, 120000);

        private Quest _missiveQuest;
        private Quest _bloomQuest;

        private int _missiveDueTick = -1;

        private Pawn _assimilated;

        public ViviEndingQuest()
        {
            Hediff_EverflowerAssimilation.ResetRegistry();
        }

        public Pawn AssimilatedPawn => _assimilated != null && Hediff_EverflowerAssimilation.IsAssimilated(_assimilated) ? _assimilated : null;

        // 바닐라는 종료된 퀘스트의 부품을 정리 후 30일이 지나면 삭제한다.
        // 축복·제단·날씨 의식에 필요한 앵커는 엔딩 후에도 유지되도록 퀘스트 부품과 별도로 보관한다.
        private ArcanePlant_Everflower _anchor;

        private List<HeldPawn> _heldPawns = new List<HeldPawn>();

        private Quest _cachedQuest;
        private int _cachedPartCount = -1;
        private QuestPart_ViviEndingState _statePart;
        private QuestPart_ViviBloomCondensation _bloomPart;
        private QuestPart_Delay _calmPart;
        private QuestPart_ViviApocrypha _apocryphaPart;

        public Quest MissiveQuest => _missiveQuest;
        public Quest BloomQuest => _bloomQuest;

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

        private bool IsFinished => Stage.IsFinishedStage();
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

        internal Building_DreamumAltar AnchorAltar => ViviEndingUtility.AltarOf(_anchor);

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

            if (_pendingHolds.Count > 0)
            {
                try
                {
                    ProcessPendingHolds();
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to hold returning pawns: {ex}");
                }
            }

            if (_heldPawns.Count > 0)
            {
                TickHeldPawns(now);
            }

            // 보관된 공명자는 틱을 처리하지 않으므로 여기서 동화 상태 판정과 연출을 처리한다.
            if (_assimilated != null)
            {
                try
                {
                    Hediff_EverflowerAssimilation.Of(_assimilated)?.TickHeld(now);
                }
                catch (Exception ex)
                {
                    Log.ErrorOnce($"[ViViRace] everflower assimilation tick failed: {ex}", 0x5645_4E30);
                }
            }

            try
            {
                TickAltarPulse(now);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] altar everflower pulse failed: {ex}", 0x5645_4E20);
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
                    EnsureMissiveRequirementsPart();
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] ending quest tick failed at stage {Stage}: {ex}", 0x5645_4E00);
            }
        }

        public void Update()
        {
            UpdateEndingScreenFade();
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

            return LocalizeString_EndingCore.VV_Inspector_EndingBloom.Translate(BloomProgress.ToStringPercent().Named("PERCENT")).Resolve();
        }

        public static bool MissiveArrivalConditionsMet()
        {
            var quest = Current;
            if (quest == null || quest.Stage != ViviEndingStage.None) { return false; }
            if (quest.LastEchoStage < MissiveMinEchoStage) { return false; }

            var everflower = ViviEndingUtility.FindEndingCandidate();
            if (everflower == null || everflower.AttunementLevel < Building_DreamumAltar.RequiredAttunementLevel) { return false; }
            if (!Building_DreamumAltar.IsValidAltarMap(everflower.MapHeld)) { return false; }

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

        private void EnsureMissiveRequirementsPart()
        {
            var quest = _missiveQuest;
            if (quest == null || quest.State != QuestState.Ongoing) { return; }
            if (quest.GetFirstPartOfType<QuestPart_ViviMissiveRequirements>() != null) { return; }

            quest.AddPart(new QuestPart_ViviMissiveRequirements());
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
                        && ritual.Ritual?.def == EndingRitualDefOf.VV_GrandAttunement
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

        public void Notify_AssimilationBroken(Pawn pawn, bool died)
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
                LocalizeString_EndingRituals.VV_Message_AssimilationBrokenRitual.Translate(pawn.Named("PAWN")),
                new LookTargets(pawn.PositionHeld, pawn.MapHeld),
                MessageTypeDefOf.NegativeEvent);
        }

        // 도착 조건은 TickMissiveArrival에서 검사하므로 여기서는 기존 밀서·본편 퀘스트 유무만 확인한다.
        public bool StartMissiveQuest(ArcanePlant_Everflower everflower)
        {
            if (_missiveQuest != null || _bloomQuest != null) { return false; }

            if (everflower == null || everflower.Destroyed)
            {
                everflower = ViviEndingUtility.FindEndingCandidate();
            }

            var slate = new Slate();
            if (everflower != null)
            {
                slate.Set("everflower", everflower);
            }
            SetPoints(slate, everflower?.MapHeld ?? Find.AnyPlayerHomeMap);

            var quest = GenerateQuest(EndingCoreDefOf.VV_EndGame_ViviMissive, slate, ViviEndingSignals.GrandAttunementCompleted);
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

            var everflower = ViviEndingUtility.FindEndingCandidate();

            var slate = new Slate();
            if (everflower != null)
            {
                slate.Set("everflower", everflower);
            }
            slate.Set("reissued", true);
            SetPoints(slate, everflower?.MapHeld ?? Find.AnyPlayerHomeMap);

            var quest = GenerateQuest(EndingCoreDefOf.VV_EndGame_ViviMissive, slate, ViviEndingSignals.GrandAttunementCompleted);
            if (quest == null) { return; }

            Log.Warning($"[ViViRace] the federation missive quest ended unexpectedly ({ended.State}); issued it again.");
            _missiveQuest = quest;
            Find.QuestManager.Add(quest);
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

            var quest = GenerateQuest(EndingCoreDefOf.VV_EndGame_ViviBloom, slate, ViviEndingSignals.AnchorLost);
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

        public void HoldPawnUntilReturn(Pawn pawn, int ticks)
        {
            if (pawn == null || pawn.Destroyed) { return; }
            if (_heldPawns.Any(v => v?.pawn == pawn)) { return; }

            var map = pawn.MapHeld ?? AnchorMap ?? Boss?.MapHeld ?? Find.AnyPlayerHomeMap;

            if (pawn.Spawned)
            {
                // 마지막 자유 정착민이 잠시라도 사라지면 게임 오버로 판정될 수 있으므로 대기 없이 제단 앞으로 보낸다.
                if (IsLastFreeColonist(pawn))
                {
                    TeleportToAltarFront(pawn);
                    return;
                }

                // 폰의 헤디프 틱에서 호출되므로 디스폰은 모든 Thing 틱이 끝난 뒤 게임 컴포넌트 틱으로 미룬다.
                if (!_pendingHolds.Any(v => v.pawn == pawn))
                {
                    _pendingHolds.Add(new PendingHold() { pawn = pawn, ticks = ticks, map = map });
                }
                return;
            }

            HoldDespawnedPawn(pawn, ticks, map);
        }

        // 같은 틱에 비우는 대기열이므로 저장하지 않는다. 일시정지 중 개발자 기즈모로 추가한 항목은 다음 틱에 처리한다.
        private class PendingHold
        {
            public Pawn pawn;
            public int ticks;
            public Map map;
        }

        private readonly List<PendingHold> _pendingHolds = new List<PendingHold>();

        private void ProcessPendingHolds()
        {
            var pending = _pendingHolds.ToList();
            _pendingHolds.Clear();

            foreach (var hold in pending)
            {
                var pawn = hold.pawn;
                if (pawn == null || pawn.Destroyed || pawn.Dead) { continue; }
                if (_heldPawns.Any(v => v?.pawn == pawn)) { continue; }

                if (pawn.Spawned)
                {
                    if (IsLastFreeColonist(pawn))
                    {
                        TeleportToAltarFront(pawn);
                        continue;
                    }

                    SkipUtility.SkipDeSpawn(pawn);
                }

                HoldDespawnedPawn(pawn, hold.ticks, hold.map ?? AnchorMap ?? Find.AnyPlayerHomeMap);
            }
        }

        private void HoldDespawnedPawn(Pawn pawn, int ticks, Map map)
        {
            if (pawn.Spawned || pawn.Destroyed) { return; }

            pawn.holdingOwner?.Remove(pawn);

            _heldPawns.Add(new HeldPawn()
            {
                pawn = pawn,
                returnTick = Find.TickManager.TicksGame + Mathf.Max(1, ticks),
                map = map,
            });
        }

        private void TickHeldPawns(int now)
        {
            // 보관 중인 정착민은 게임 오버 판정에서 제외되므로 남은 정착민이 모두 쓰러지면 귀환 대기를 생략한다.
            // 즉시 스폰하면 바닐라가 재판정해 400틱의 게임 오버 카운트다운을 취소한다.
            var gameEnding = Find.GameEnder.gameEnding;
            for (int i = _heldPawns.Count - 1; i >= 0; --i)
            {
                var held = _heldPawns[i];
                if (held?.pawn == null || held.pawn.Destroyed)
                {
                    _heldPawns.RemoveAt(i);
                    continue;
                }

                if (now < held.returnTick && !(gameEnding && held.pawn.IsFreeColonist)) { continue; }

                _heldPawns.RemoveAt(i);
                try
                {
                    ReturnHeldPawn(held);
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to return held pawn {held.pawn}: {ex}");

                    var pawn = held.pawn;
                    if (!pawn.Spawned && !pawn.Destroyed && pawn.holdingOwner == null && !Find.WorldPawns.Contains(pawn))
                    {
                        held.returnTick = now + HeldPawnRetryTicks;
                        _heldPawns.Add(held);
                    }
                }
            }
        }

        private void ReturnHeldPawn(HeldPawn held)
        {
            var pawn = held.pawn;
            var map = held.map;
            if (map == null || !Find.Maps.Contains(map))
            {
                map = AnchorMap ?? Find.AnyPlayerHomeMap ?? FindMapWithFreeColonists();
            }

            if (map == null)
            {
                var caravan = Find.WorldObjects.Caravans.FirstOrDefault(v => v.IsPlayerControlled);
                if (caravan != null)
                {
                    caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: true);
                    if (!pawn.IsWorldPawn())
                    {
                        Find.WorldPawns.PassToWorld(pawn);
                    }

                    Messages.Message(
                        LocalizeString_EndingCore.VV_Message_EndingPawnReturnedToCaravan.Translate(pawn.Named("PAWN"), caravan.Named("CARAVAN")),
                        caravan,
                        MessageTypeDefOf.PositiveEvent);
                    return;
                }

                Find.WorldPawns.PassToWorld(pawn);
                return;
            }

            var cell = FindReturnCell(map);
            pawn.holdingOwner?.Remove(pawn);
            GenSpawn.Spawn(pawn, cell, map);
            pawn.Notify_Teleported();

            if (pawn.drafter != null && pawn.wasDraftedBeforeSkip)
            {
                pawn.drafter.Drafted = true;
            }

            PlayReturnEffect(pawn, map);

            Messages.Message(
                LocalizeString_EndingCore.VV_Message_EndingPawnReturned.Translate(pawn.Named("PAWN")),
                pawn,
                MessageTypeDefOf.PositiveEvent);
        }

        private static Map FindMapWithFreeColonists()
        {
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; ++i)
            {
                if (maps[i].mapPawns.FreeColonistsSpawnedCount > 0)
                {
                    return maps[i];
                }
            }

            return null;
        }

        private static void PlayReturnEffect(Pawn pawn, Map map)
        {
            var returnEffect = ApocryphaGimmickDefOf.VV_Effecter_FairyReturn ?? EffecterDefOf.Skip_ExitNoDelay;
            returnEffect?.SpawnMaintained(pawn, map);
        }

        private static bool IsLastFreeColonist(Pawn pawn)
        {
            if (!pawn.IsFreeColonist) { return false; }

            var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; ++i)
            {
                if (colonists[i] != pawn)
                {
                    return false;
                }
            }

            return true;
        }

        private void TeleportToAltarFront(Pawn pawn)
        {
            var map = pawn.Map;
            var cell = FindReturnCell(map);
            if (!cell.IsValid || cell == pawn.Position) { return; }

            EffecterDefOf.Skip_EntryNoDelay.Spawn(pawn, map).Cleanup();
            pawn.Position = cell;
            pawn.Notify_Teleported();
            PlayReturnEffect(pawn, map);

            Messages.Message(
                LocalizeString_EndingCore.VV_Message_EndingPawnReturned.Translate(pawn.Named("PAWN")),
                pawn,
                MessageTypeDefOf.PositiveEvent);
        }

        private IntVec3 FindReturnCell(Map map)
        {
            var altar = AnchorAltar;
            var root = altar != null && altar.Spawned && altar.Map == map ? altar.FrontCell : DropCellFinder.TradeDropSpot(map);

            var cell = CellFinder.StandableCellNear(root, map, ReturnCellSearchRadius, c => c.InBounds(map) && !c.Fogged(map));
            if (!cell.IsValid)
            {
                cell = CellFinder.RandomClosewalkCellNear(root, map, 12);
            }

            return cell;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref _missiveQuest, "missiveQuest");
            Scribe_References.Look(ref _bloomQuest, "bloomQuest");
            Scribe_References.Look(ref _anchor, "anchorEverflower");
            Scribe_References.Look(ref _amberCoreQuest, "amberCoreQuest");
            Scribe_Values.Look(ref _lastEchoStage, "lastEchoStage", 0);
            Scribe_Values.Look(ref _missiveDueTick, "missiveDueTick", -1);
            Scribe_References.Look(ref _assimilated, "assimilatedPawn");
            Scribe_Collections.Look(ref _heldPawns, "heldPawns", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_heldPawns == null)
                {
                    _heldPawns = new List<HeldPawn>();
                }

                _heldPawns.RemoveAll(v => v?.pawn == null);
            }
        }
    }
}
