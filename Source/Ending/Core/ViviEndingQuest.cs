using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 엔딩 〈만개하는 여명〉의 창구. 진행은 바닐라 퀘스트 두 개가 맡는다.
    //   〈비비 연방의 밀서〉(VV_EndGame_ViviMissive, QuestNode_Root_ViviMissive): 서신·제단 연구 해금·고대 호박 코어
    //   〈만개하는 여명〉(VV_EndGame_ViviBloom, QuestNode_Root_ViviBloom): 공성 → 평화 기간 → 아포크리파 → 여왕의 대관식 → 엔딩
    // 새 게임 컴포넌트를 만들지 않고 GameComponent_Mana가 보관·저장·틱 처리한다. 다른 모듈이 쓰는 질의·통지 API를 두 퀘스트의 상태와 신호로 옮기고,
    // 퀘스트 밖에서 이어져야 하는 일(4단계 감시와 서신 예약, 결속 해방 후 잠시 사라진 비비의 귀환, 제단 맥동·섬광 연출)을 맡는다.
    // 연출은 EndingAmbience, 개발자 도구는 EndingDebug partial 파일에 있다.
    public partial class ViviEndingQuest : IExposable
    {
        // 결속 해방 후 잠시 맵에서 사라진 비비. 디스폰된 폰을 여기서만 Deep 저장하고(월드 폰에 넘기지 않음) 귀환 틱에 제단 앞에 다시 스폰한다.
        public class HeldPawn : IExposable
        {
            public Pawn pawn;
            public int returnTick;
            public Map map;

            public void ExposeData()
            {
                Scribe_Deep.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref returnTick, "returnTick", 0);
                Scribe_References.Look(ref map, "map");
            }
        }

        public static ViviEndingQuest Current => Verse.Current.Game?.GetComponent<GameComponent_Mana>()?.EndingQuest;

        private const int StageTickInterval = 250;
        private const int ScanIntervalTicks = 2500;
        private const int LetterTriggerLevel = 4;
        private const int QuestRetryTicks = 2500;
        private const float ReturnCellSearchRadius = 8f;
        private const int HeldPawnRetryTicks = 2500;
        private static readonly IntRange FederationLetterDelayTicks = new IntRange(60000, 120000);

        private Quest _missiveQuest;
        private Quest _bloomQuest;

        // 앵커 영원꽃(5단계 대조율을 치른 개체). 바닐라는 끝난 퀘스트의 부품을 정리 30일 뒤 지우므로,
        // 엔딩 이후에도 쓰는 앵커(축복·제단·날씨 의식)는 퀘스트 부품과 별도로 여기서 들고 있는다.
        private ArcanePlant_Everflower _anchor;

        // 4단계 감지 후 서신(밀서 퀘스트 생성) 예정 틱. 예약이 없으면 -1.
        private int _letterTick = -1;
        private List<HeldPawn> _heldPawns = new List<HeldPawn>();

        // 비저장: 본편 퀘스트 부품 캐시(퀘스트가 바뀌거나 부품 목록이 지워지면 다시 찾는다)
        private Quest _cachedQuest;
        private int _cachedPartCount = -1;
        private QuestPart_ViviEndingState _statePart;
        private QuestPart_ViviBloomCondensation _bloomPart;
        private QuestPart_Delay _calmPart;
        private QuestPart_ViviApocrypha _apocryphaPart;

        public Quest MissiveQuest => _missiveQuest;
        public Quest BloomQuest => _bloomQuest;

        // 진행 단계: 본편 퀘스트가 있으면 그 상태(끝났으면 성공 → Completed, 그 외 → Failed), 밀서 퀘스트만 있으면 AltarUnlocked,
        // 서신 예약 중이면 LetterPending, 그 외 None. 단계 값은 진행 순서와 다르므로(Coronation을 뒤에 추가) 대소 비교를 쓰지 않는다.
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

                if (_missiveQuest != null) { return ViviEndingStage.AltarUnlocked; }

                return _letterTick >= 0 ? ViviEndingStage.LetterPending : ViviEndingStage.None;
            }
        }

        // 6단계 개화 응축 진행도(0~1). Siege 이후에만 의미가 있으며 평화 기간·보스 단계에는 99%, 토벌 뒤에는 100%다.
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

        // 엔딩이 묶인 제단 위 영원꽃(5단계 대조율을 완료한 개체). Siege 이전에는 null.
        public ArcanePlant_Everflower AnchorEverflower => _anchor;
        public Map AnchorMap => _anchor?.MapHeld;

        public Pawn Boss => _bloomQuest != null && _bloomQuest.State == QuestState.Ongoing ? ApocryphaPart?.Boss : null;
        public CompApocrypha BossComp => Boss?.GetComp<CompApocrypha>();

        // 공성 기간(의식 품질로 정한 8~10일). 본편 퀘스트 전에는 0.
        public int SiegeDurationTicks => BloomPart?.DurationTicks ?? 0;

        public bool IsAltarUnlocked => Stage != ViviEndingStage.None && Stage != ViviEndingStage.LetterPending;
        public bool IsEndingActive
        {
            get
            {
                var stage = Stage;
                return stage == ViviEndingStage.Siege || stage == ViviEndingStage.Calm || stage == ViviEndingStage.Boss || stage == ViviEndingStage.Coronation;
            }
        }
        public bool IsCompleted => Stage == ViviEndingStage.Completed;

        // 아포크리파를 토벌해 응축이 100%가 되었고, 공명자의 〈여왕의 대관식〉만 남은 상태.
        public bool IsAwaitingCoronation => Stage == ViviEndingStage.Coronation;

        // 엔딩이 끝났는지(완료·실패).
        private bool IsFinished => Stage == ViviEndingStage.Completed || Stage == ViviEndingStage.Failed;
        public bool IsBossPhase2 => Stage == ViviEndingStage.Boss && BossComp != null && BossComp.Phase == ApocryphaPhase.Unbound;

        public bool IsAnchor(ArcanePlant_Everflower everflower) => everflower != null && everflower == _anchor;

        // 평화 기간/보스전 동안 해당 맵의 일반 위협(습격 등) 발생을 막아야 하는지.
        // 보스가 등장할 길을 찾지 못해 미뤄지는 동안에는 억제를 풀어 정상적인 위협이 다시 찾아오게 한다.
        public bool ShouldSuppressThreats(Map map)
        {
            if (map == null || map != AnchorMap || IsBossDeferred) { return false; }

            var stage = Stage;
            return stage == ViviEndingStage.Calm || stage == ViviEndingStage.Boss;
        }

        // 앵커 맵에 습격 비콘(Map_RaidBeacon)을 붙일지. 바닐라 우주선 반응로 가동처럼 공성 시작부터 아포크리파 토벌까지 유지된다.
        // 평화 기간·보스전에는 위협 억제(ShouldSuppressThreats)가 우선하므로 실제 습격은 공성 중이거나 보스 등장이 미뤄진 동안에만 발생한다.
        public bool IsRaidBeaconActive(Map map)
        {
            if (map == null || map != AnchorMap) { return false; }

            var stage = Stage;
            return stage == ViviEndingStage.Siege || stage == ViviEndingStage.Calm || stage == ViviEndingStage.Boss;
        }

        // 평화 기간 남은 틱(평화 타이머가 돌고 있지 않으면 0).
        internal int CalmTicksLeft => Stage == ViviEndingStage.Calm && CalmPart != null && CalmPart.State == QuestPartState.Enabled ? Mathf.Max(0, CalmPart.TicksLeft) : 0;

        // 평화 기간이 끝났는데 아포크리파가 등장할 길을 찾지 못해 미뤄지고 있는지.
        internal bool IsBossDeferred => ApocryphaPart != null && ApocryphaPart.State == QuestPartState.Enabled && ApocryphaPart.IsDeferred;

        // 앵커가 안치된 제단(앵커가 제단 밖으로 밀려났으면 앵커 맵의 제단).
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

        // GameComponent_Mana.GameComponentTick에서 매 틱 호출된다. 보류 중인 폰의 귀환과 제단 맥동은 매 틱, 서신 예약은 250틱 간격으로 처리한다.
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

            try
            {
                TickAltarPulse(now);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] altar everflower pulse failed: {ex}", 0x5645_4E20);
            }

            if (now % StageTickInterval != 0) { return; }

            try
            {
                switch (Stage)
                {
                    case ViviEndingStage.None:
                        if (now % ScanIntervalTicks == 0)
                        {
                            ScanForAwakenedEverflower();
                        }
                        break;

                    case ViviEndingStage.LetterPending:
                        if (now >= _letterTick)
                        {
                            SendFederationLetter(null);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] ending quest tick failed at stage {Stage}: {ex}", 0x5645_4E00);
            }
        }

        // GameComponent_Mana.GameComponentUpdate에서 매 프레임 호출된다(일시정지 중에도). 실시간 연출만 처리한다.
        public void Update()
        {
            UpdateEndingScreenFade();
        }

        // [A 호출] CompEverflower.ChangeAttunementLevel 직후. 플레이어 영원꽃이 4단계 이상이 되면 연방 서신을 예약한다.
        public void Notify_EverflowerLevelChanged(ArcanePlant_Everflower everflower, int level)
        {
            if (everflower == null || level < LetterTriggerLevel) { return; }
            if (Stage != ViviEndingStage.None) { return; }
            if (everflower.Faction != Faction.OfPlayer) { return; }

            ScheduleFederationLetter();
        }

        // [B 호출] 5단계 대조율 의식이 완료되어 영원꽃이 5단계가 된 직후 → 밀서 퀘스트 성공, 〈만개하는 여명〉 시작.
        public void Notify_GrandAttunementCompleted(ArcanePlant_Everflower everflower, float quality)
        {
            if (everflower == null) { return; }

            var stage = Stage;
            if (stage != ViviEndingStage.AltarUnlocked)
            {
                Log.Warning($"[ViViRace] grand attunement completed at ending stage {stage}; the siege only starts from {ViviEndingStage.AltarUnlocked}.");
                return;
            }

            StartBloomQuest(everflower, quality);
        }

        // [D 호출] 아포크리파 소멸 연출이 끝났을 때 → 응축 100%, 〈여왕의 대관식〉 해금. 소멸 연출 Thing이 저장/불러오기를 거치면 boss는 null일 수 있다.
        public void Notify_BossDefeated(Pawn boss)
        {
            if (Stage != ViviEndingStage.Boss) { return; }

            ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.ApocryphaDissolved);
        }

        // [B 호출] 앵커 영원꽃에서 〈여왕의 대관식〉이 끝났을 때 → 6단계 개화 연출 후 곧이어 엔딩 크레딧.
        public void Notify_CoronationCompleted(ArcanePlant_Everflower everflower, Pawn queen)
        {
            if (Stage != ViviEndingStage.Coronation || !IsAnchor(everflower)) { return; }

            ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.CoronationCompleted, queen.Named("PAWN"));
        }

        // [D 호출] 아포크리파가 영원꽃 채널링을 끝냈을 때 → 실패(편지·정리는 퀘스트). 영원꽃 파괴와 보스 퇴장은 D가 이 호출 뒤에 한다.
        public void Notify_EverflowerStolen(Pawn boss)
        {
            if (!IsEndingActive) { return; }

            ViviEndingSignals.Send(_bloomQuest, ViviEndingSignals.EverflowerStolen);
        }

        // [A 호출] 영원꽃의 CompGetGizmosExtra에서 그대로 yield한다(개화 응축 진행 막대, DEV 단계 조작 기즈모).
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

        // [A 호출] 영원꽃의 CompInspectStringExtra에 덧붙일 엔딩 진행 문구(없으면 null).
        public string GetEverflowerInspectString(ArcanePlant_Everflower everflower)
        {
            if (!IsAnchor(everflower)) { return null; }

            var status = GetStatusText();
            if (status == null) { return null; }

            var bloom = LocalizeString_EndingCore.VV_Inspector_EndingBloom.Translate(BloomProgress.ToStringPercent().Named("PERCENT")).Resolve();
            return bloom + "\n" + status;
        }

        // 현재 단계 안내 문구(정보창·퀘스트 탭 설명). 엔딩이 진행 중이거나 완료됐을 때만.
        // 퀘스트 탭에는 평화 타이머의 남은 시간 줄이 따로 있으므로 평화 기간 남은 시간은 넣지 않는다.
        internal string GetStatusText(bool forQuestTab = false)
        {
            switch (Stage)
            {
                case ViviEndingStage.Siege:
                    return LocalizeString_EndingCore.VV_Inspector_EndingSiege.Translate();

                case ViviEndingStage.Calm:
                    if (IsBossDeferred)
                    {
                        return LocalizeString_EndingCore.VV_Inspector_EndingCalmDeferred.Translate().Resolve();
                    }

                    return forQuestTab ? null : LocalizeString_EndingCore.VV_Inspector_EndingCalm.Translate(CalmTicksLeft.ToStringTicksToPeriod().Named("TIME")).Resolve();

                case ViviEndingStage.Boss:
                    return LocalizeString_EndingCore.VV_Inspector_EndingBlocked.Translate();

                case ViviEndingStage.Coronation:
                    return LocalizeString_EndingCore.VV_Inspector_EndingCoronation.Translate();

                case ViviEndingStage.Completed:
                    return LocalizeString_EndingCore.VV_Inspector_EndingFullBloom.Translate();

                default:
                    return null;
            }
        }

        // 이미 4단계인 기존 세이브는 레벨 변경 알림이 다시 오지 않으므로 None 단계에서 주기적으로 맵을 훑는다.
        private void ScanForAwakenedEverflower()
        {
            var everflower = ViviEndingUtility.FindEndingCandidate();
            if (everflower != null && everflower.AttunementLevel >= LetterTriggerLevel)
            {
                ScheduleFederationLetter();
            }
        }

        private void ScheduleFederationLetter()
        {
            _letterTick = Find.TickManager.TicksGame + FederationLetterDelayTicks.RandomInRange;
        }

        // 서신 도착: 〈비비 연방의 밀서〉 퀘스트를 만든다(서신 편지·제단 연구 해금·호박 코어 전달은 퀘스트가 맡는다).
        private void SendFederationLetter(ArcanePlant_Everflower everflower)
        {
            if (_missiveQuest != null)
            {
                _letterTick = -1;
                return;
            }

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

            var quest = GenerateQuest(EndingCoreDefOf.VV_EndGame_ViviMissive, slate);
            if (quest == null)
            {
                // 생성 오류(이미 기록됨): 잠시 뒤 다시 시도한다.
                _letterTick = Find.TickManager.TicksGame + QuestRetryTicks;
                return;
            }

            _missiveQuest = quest;
            _letterTick = -1;
            Find.QuestManager.Add(quest);
        }

        // 대조율 의식 완료: 〈만개하는 여명〉을 앵커와 함께 만들고 밀서 퀘스트를 성공으로 끝낸다.
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
            SetPoints(slate, map);

            var quest = GenerateQuest(EndingCoreDefOf.VV_EndGame_ViviBloom, slate);
            if (quest == null) { return false; }

            // 퀘스트가 추가되는 순간 시작 신호가 처리되므로 창구 상태를 먼저 맞춘다.
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

        // 바닐라 QuestUtility.GenerateQuestAndMakeAvailable에서 퀘스트 추가만 뺀 것. 생성에 실패하면(QuestGen이 오류를 기록) null.
        private static Quest GenerateQuest(QuestScriptDef def, Slate slate)
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
            }

            return quest;
        }

        // [E 호출] 요정 간섭 비비가 결속을 해방한 뒤 맵에서 잠시 사라졌다가 제단 앞으로 돌아오게 한다.
        // 스폰된 폰을 넘기면 여기서 스킵 연출과 함께 디스폰한다(이미 디스폰된 폰도 받는다). Scribe_Deep으로 보관하며,
        // ticks 뒤 제단 앞(또는 맵의 교역 낙하 지점) 근처에 다시 스폰한다. 마지막 자유 정착민이면 보류하지 않고 바로 제단 앞으로 보낸다.
        public void HoldPawnUntilReturn(Pawn pawn, int ticks)
        {
            if (pawn == null || pawn.Destroyed) { return; }
            if (_heldPawns.Any(v => v?.pawn == pawn)) { return; }

            // 돌아올 맵: 폰이 있던 맵(보통 보스·앵커 맵). 이미 디스폰된 폰이면 엔딩 맵(앵커 → 보스) 순으로 정한다.
            var map = pawn.MapHeld ?? AnchorMap ?? Boss?.MapHeld ?? Find.AnyPlayerHomeMap;

            if (pawn.Spawned)
            {
                // 이 비비가 마지막 자유 정착민이면 잠시라도 사라졌을 때 게임 오버 판정이 날 수 있으므로 보류하지 않고 바로 제단 앞으로 보낸다.
                if (IsLastFreeColonist(pawn))
                {
                    TeleportToAltarFront(pawn);
                    return;
                }

                // 요정 간섭 헤디프의 틱(폰 자신의 틱) 안에서 불리므로 실제 디스폰은 이번 틱의 게임 컴포넌트 틱(모든 Thing 틱 이후)으로 미룬다.
                if (!_pendingHolds.Any(v => v.pawn == pawn))
                {
                    _pendingHolds.Add(new PendingHold() { pawn = pawn, ticks = ticks, map = map });
                }
                return;
            }

            HoldDespawnedPawn(pawn, ticks, map);
        }

        // 저장하지 않는 대기열: 같은 틱 안에 처리되므로 저장 시점에는 항상 비어 있다(일시정지 중 개발자 기즈모로 넣은 경우는 다음 틱).
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

                    // 스킵 연출과 함께 사라진다(SkipDeSpawn은 들고 있던 물건을 내려놓고 징집 상태를 기억한다).
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
            // 맵에 남은 정착민이 모두 쓰러져 바닐라 게임 오버 카운트다운(400틱)이 시작되면, 보류 중인 정착민은 판정 대상에 포함되지 않으므로
            // 기다리지 않고 곧바로 돌려보낸다. 스폰되는 순간 바닐라가 다시 판정하므로 카운트다운이 취소된다.
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

                    // 스폰 전에 실패했다면 폰을 잃지 않도록 다시 보관하고 잠시 뒤 재시도한다.
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
                map = AnchorMap ?? Find.AnyPlayerHomeMap;
            }

            if (map == null)
            {
                // 돌아갈 맵이 모두 사라졌다면 폰을 잃지 않도록 월드 폰으로 넘긴다.
                Find.WorldPawns.PassToWorld(pawn);
                return;
            }

            var cell = FindReturnCell(map);
            pawn.holdingOwner?.Remove(pawn);
            GenSpawn.Spawn(pawn, cell, map);
            pawn.Notify_Teleported();

            // SkipUtility.SkipDeSpawn이 기억해 둔 징집 상태를 되살린다(바닐라 SkipTo와 같은 처리).
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

        private static void PlayReturnEffect(Pawn pawn, Map map)
        {
            var returnEffect = DefDatabase<EffecterDef>.GetNamedSilentFail("VV_Effecter_FairyReturn") ?? EffecterDefOf.Skip_ExitNoDelay;
            returnEffect?.SpawnMaintained(pawn, map);
        }

        // 맵 위의 마지막 자유 정착민인지(그 폰이 잠시라도 사라지면 바닐라 게임 오버 판정이 시작될 수 있다).
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

        // 보류 없이 곧바로 제단 앞으로 순간이동시킨다(스킵 연출 포함).
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
            Scribe_Values.Look(ref _letterTick, "letterTick", -1);
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
