using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 엔딩 퀘스트 상태 머신. 새 게임 컴포넌트를 만들지 않고 GameComponent_Mana가 보관·저장하고 틱을 처리한다.
    // [계약] public 시그니처는 다른 모듈이 호출하므로 바꾸지 말 것.
    // 단계별 본문은 같은 폴더의 partial 파일로 나눈다: 서신·호박 코어(EndingFederation), 공성(EndingSiege), 평화·보스 등장(EndingCalm), 대관식·엔딩·실패(EndingCelebration).
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
        private const float BloomLockedProgress = 0.99f;
        private const float DebugRitualQuality = 5f;
        private const float ReturnCellSearchRadius = 8f;
        private const int HeldPawnRetryTicks = 2500;

        private ViviEndingStage _stage = ViviEndingStage.None;
        private float _bloomProgress;
        private ArcanePlant_Everflower _anchor;
        private Pawn _boss;

        private int _letterTick = -1;
        private bool _coreDelivered;
        private int _lastCoreDeliveryTick = -1;
        private int _siegeStartTick = -1;
        private int _siegeDurationTicks;
        private int _nextReinforceTick = -1;
        private int _calmEndTick = -1;
        private bool _bossDeferred;
        private bool _bossDeferredMessageSent;
        private int _endingEffectTick = -1;
        private int _creditsTick = -1;
        private Pawn _queen;
        private List<HeldPawn> _heldPawns = new List<HeldPawn>();

        // 비저장 런타임 값: 앵커가 파괴된 뒤에도 정리할 맵, 보스 실종 감지 시작 틱
        private Map _lastAnchorMap;
        private int _bossMissingSinceTick = -1;

        public ViviEndingStage Stage => _stage;

        // 6단계 개화 응축 진행도(0~1). Siege 이후에만 의미가 있으며 Boss 단계에서는 0.99에 고정된다.
        public float BloomProgress => _bloomProgress;

        // 엔딩이 묶인 제단 위 영원꽃(5단계 대조율을 완료한 개체). Siege 이전에는 null.
        public ArcanePlant_Everflower AnchorEverflower => _anchor;
        public Map AnchorMap => _anchor?.MapHeld;

        public Pawn Boss => _boss;
        public CompApocrypha BossComp => _boss?.GetComp<CompApocrypha>();

        public bool IsAltarUnlocked => _stage != ViviEndingStage.None && _stage != ViviEndingStage.LetterPending;
        public bool IsEndingActive => _stage == ViviEndingStage.Siege || _stage == ViviEndingStage.Calm || _stage == ViviEndingStage.Boss || _stage == ViviEndingStage.Coronation;
        public bool IsCompleted => _stage == ViviEndingStage.Completed;

        // 아포크리파를 토벌해 응축이 100%가 되었고, 공명자의 〈여왕의 대관식〉만 남은 상태.
        public bool IsAwaitingCoronation => _stage == ViviEndingStage.Coronation;

        // 엔딩이 끝났는지(완료·실패). 단계 값은 진행 순서와 다르므로(Coronation을 뒤에 추가) 대소 비교를 쓰지 않는다.
        private bool IsFinished => _stage == ViviEndingStage.Completed || _stage == ViviEndingStage.Failed;
        public bool IsBossPhase2 => _stage == ViviEndingStage.Boss && BossComp != null && BossComp.Phase == ApocryphaPhase.Unbound;

        public bool IsAnchor(ArcanePlant_Everflower everflower) => everflower != null && everflower == _anchor;

        // 평화 기간/보스전 동안 해당 맵의 일반 위협(습격 등) 발생을 막아야 하는지.
        // 보스가 등장할 길을 찾지 못해 미뤄지는 동안(_bossDeferred)에는 억제를 풀어 정상적인 위협이 다시 찾아오게 한다.
        public bool ShouldSuppressThreats(Map map) => map != null && !_bossDeferred && map == AnchorMap && (_stage == ViviEndingStage.Calm || _stage == ViviEndingStage.Boss);

        // 앵커 맵에 습격 비콘(Map_RaidBeacon)을 붙일지. 바닐라 우주선 반응로 가동처럼 공성 시작부터 아포크리파 토벌까지 유지된다.
        // 평화 기간·보스전에는 위협 억제(ShouldSuppressThreats)가 우선하므로 실제 습격은 공성 중이거나 보스 등장이 미뤄진 동안에만 발생한다.
        public bool IsRaidBeaconActive(Map map) => map != null && map == AnchorMap && (_stage == ViviEndingStage.Siege || _stage == ViviEndingStage.Calm || _stage == ViviEndingStage.Boss);

        // 앵커가 안치된 제단(앵커가 제단 밖으로 밀려났으면 앵커 맵의 제단).
        internal Building_DreamumAltar AnchorAltar
        {
            get
            {
                var altar = _anchor != null && !_anchor.Destroyed ? _anchor.EverflowerComp?.EnshrinedAltar : null;
                if (altar != null) { return altar; }

                var map = AnchorMap;
                return map != null ? Building_DreamumAltar.FindOnMap(map) : null;
            }
        }

        private bool IsBossAlive => _boss != null && !_boss.Destroyed && !_boss.Dead && _boss.Spawned;

        private Pawn AnchorResonator => _anchor?.EverflowerComp?.LinkedPawns?.FirstOrDefault();

        // GameComponent_Mana.GameComponentTick에서 매 틱 호출된다. 엔딩 연출과 보류 중인 폰의 귀환만 매 틱 처리하고, 단계별 처리는 250틱 간격으로 수행한다.
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

            if (_endingEffectTick >= 0)
            {
                try
                {
                    TickEndingEffects(now);
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] ending effects failed: {ex}");
                    _endingEffectTick = -1;
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

            if (_creditsTick >= 0 && now >= _creditsTick)
            {
                _creditsTick = -1;
                SendEndingLetterAndCredits();
            }

            if (now % StageTickInterval != 0) { return; }

            try
            {
                switch (_stage)
                {
                    case ViviEndingStage.None:
                        if (now % ScanIntervalTicks == 0)
                        {
                            ScanForAwakenedEverflower();
                        }
                        break;

                    case ViviEndingStage.LetterPending:
                        if (_letterTick < 0)
                        {
                            ScheduleFederationLetter();
                        }
                        else if (now >= _letterTick)
                        {
                            SendFederationLetter(null);
                        }
                        break;

                    case ViviEndingStage.AltarUnlocked:
                        TickCoreDelivery();
                        break;

                    case ViviEndingStage.Siege:
                        TickSiege(now);
                        break;

                    case ViviEndingStage.Calm:
                        TickCalm(now);
                        break;

                    case ViviEndingStage.Boss:
                        TickBoss(now);
                        break;

                    case ViviEndingStage.Coronation:
                        if (CheckAnchorAlive())
                        {
                            EnsureGoldenBloom();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] ending quest tick failed at stage {_stage}: {ex}", 0x5645_4E00 + (int)_stage);
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
            if (_stage != ViviEndingStage.None) { return; }
            if (everflower.Faction != Faction.OfPlayer) { return; }

            ScheduleFederationLetter();
        }

        // [A 호출] 영원꽃이 제단 중앙 칸에 안치되었을 때(순간이동 완료/재설치). 엔딩 진행은 안치 여부와 무관하므로 맵만 기억한다.
        public void Notify_Enshrined(ArcanePlant_Everflower everflower, Building_DreamumAltar altar)
        {
            if (IsAnchor(everflower) && altar != null && altar.Spawned)
            {
                _lastAnchorMap = altar.Map;
            }
        }

        // [B 호출] 5단계 대조율 의식이 완료되어 영원꽃이 5단계가 된 직후 → Siege 시작.
        public void Notify_GrandAttunementCompleted(ArcanePlant_Everflower everflower, float quality)
        {
            if (everflower == null) { return; }
            if (_stage != ViviEndingStage.AltarUnlocked)
            {
                Log.Warning($"[ViViRace] grand attunement completed at ending stage {_stage}; the siege only starts from {ViviEndingStage.AltarUnlocked}.");
                return;
            }

            StartSiege(everflower, quality);
        }

        // [D 호출] 아포크리파 소멸 연출이 끝났을 때 → 응축 100%, 〈여왕의 대관식〉 해금. 소멸 연출 Thing이 저장/불러오기를 거치면 boss는 null일 수 있다.
        public void Notify_BossDefeated(Pawn boss)
        {
            if (_stage != ViviEndingStage.Boss) { return; }

            EnterCoronation();
        }

        // [B 호출] 앵커 영원꽃에서 〈여왕의 대관식〉이 끝났을 때 → 6단계 개화 연출 후 곧이어 엔딩 크레딧.
        public void Notify_CoronationCompleted(ArcanePlant_Everflower everflower, Pawn queen)
        {
            if (_stage != ViviEndingStage.Coronation || !IsAnchor(everflower)) { return; }

            CompleteEnding(removeBoss: false, queen);
        }

        // [D 호출] 아포크리파가 영원꽃 채널링을 끝냈을 때 → 상태·편지만 처리한다. 영원꽃 파괴와 보스 퇴장은 D가 이 호출 전후에 한다.
        public void Notify_EverflowerStolen(Pawn boss)
        {
            if (!IsEndingActive) { return; }

            FailEnding(EndingFailReason.Stolen, removeBoss: false);
        }

        // [A 호출] 엔딩 중 앵커 영원꽃이 다른 이유로 파괴되었을 때(ArcanePlant_Everflower.Destroy에서 base.Destroy 전에 — 맵이 아직 유효).
        // 찬탈로 이미 Failed가 된 경우는 무시한다.
        public void Notify_AnchorDestroyed(ArcanePlant_Everflower everflower)
        {
            if (!IsAnchor(everflower) || !IsEndingActive) { return; }

            if (everflower.Spawned)
            {
                _lastAnchorMap = everflower.Map;
            }

            FailEnding(EndingFailReason.AnchorLost, removeBoss: true);
        }

        // [A 호출] 영원꽃의 CompGetGizmosExtra에서 그대로 yield한다(개화 응축 진행 막대, DEV 단계 조작 기즈모).
        public IEnumerable<Gizmo> GetEverflowerGizmos(ArcanePlant_Everflower everflower)
        {
            if (everflower == null) { yield break; }

            if (IsAnchor(everflower) && IsEndingActive)
            {
                yield return new Gizmo_ViviEndingBloom(this);
            }

            if (!DebugSettings.godMode || everflower.Faction != Faction.OfPlayer) { yield break; }

            // 엔딩이 영원꽃에 묶인 뒤에는 앵커에만 단계 조작 기즈모를 보여준다.
            if (_anchor != null && !_anchor.Destroyed && !IsAnchor(everflower)) { yield break; }

            if (!IsAltarUnlocked)
            {
                yield return DevCommand("DEV: Send federation letter now", () => SendFederationLetter(everflower));
            }

            if (IsAltarUnlocked && !IsFinished)
            {
                yield return DevCommand("DEV: Deliver amber core now", () => DeliverAmberCore(everflower.MapHeld));
            }

            if (!IsFinished)
            {
                var next = NextStage(_stage);
                yield return DevCommand($"DEV: Ending stage → {next}", () => DebugSetStage(next, everflower));
            }

            if (_stage == ViviEndingStage.Siege)
            {
                yield return DevCommand("DEV: Bloom +10%", () => DebugSetBloom(_bloomProgress + 0.1f));
                yield return DevCommand("DEV: Bloom → 99%", () => DebugSetBloom(BloomLockedProgress));
            }

            if (_stage == ViviEndingStage.Calm)
            {
                yield return DevCommand("DEV: End calm now", DebugEndCalm);
            }

            if (!IsFinished && !IsBossAlive)
            {
                yield return DevCommand("DEV: Spawn Apocrypha now", () => DebugSpawnBossNow(everflower));
            }

            if (!IsFinished)
            {
                yield return DevCommand("DEV: Complete ending", () => DebugSetStage(ViviEndingStage.Completed, everflower));
                yield return DevCommand("DEV: Fail ending", () => DebugSetStage(ViviEndingStage.Failed, everflower));
            }
        }

        // [A 호출] 영원꽃의 CompInspectStringExtra에 덧붙일 엔딩 진행 문구(없으면 null).
        public string GetEverflowerInspectString(ArcanePlant_Everflower everflower)
        {
            if (!IsAnchor(everflower)) { return null; }

            string status;
            switch (_stage)
            {
                case ViviEndingStage.Siege:
                    status = LocalizeString_EndingCore.VV_Inspector_EndingSiege.Translate();
                    break;

                case ViviEndingStage.Calm:
                    status = _bossDeferred
                        ? LocalizeString_EndingCore.VV_Inspector_EndingCalmDeferred.Translate().Resolve()
                        : LocalizeString_EndingCore.VV_Inspector_EndingCalm.Translate(CalmTicksLeft.ToStringTicksToPeriod().Named("TIME")).Resolve();
                    break;

                case ViviEndingStage.Boss:
                    status = LocalizeString_EndingCore.VV_Inspector_EndingBlocked.Translate();
                    break;

                case ViviEndingStage.Coronation:
                    status = LocalizeString_EndingCore.VV_Inspector_EndingCoronation.Translate();
                    break;

                case ViviEndingStage.Completed:
                    status = LocalizeString_EndingCore.VV_Inspector_EndingFullBloom.Translate();
                    break;

                default:
                    return null;
            }

            var bloom = LocalizeString_EndingCore.VV_Inspector_EndingBloom.Translate(_bloomProgress.ToStringPercent().Named("PERCENT")).Resolve();
            return bloom + "\n" + status;
        }

        // 평화 기간 남은 틱(평화 기간이 아니면 0).
        internal int CalmTicksLeft => _stage == ViviEndingStage.Calm && _calmEndTick >= 0 ? Mathf.Max(0, _calmEndTick - Find.TickManager.TicksGame) : 0;

        internal bool IsBossDeferred => _bossDeferred;

        // [E 호출] 요정 간섭 비비가 결속을 해방한 뒤 맵에서 잠시 사라졌다가 제단 앞으로 돌아오게 한다.
        // 스폰된 폰을 넘기면 여기서 스킵 연출과 함께 디스폰한다(이미 디스폰된 폰도 받는다). Scribe_Deep으로 보관하며,
        // ticks 뒤 제단 앞(또는 맵의 교역 낙하 지점) 근처에 다시 스폰한다. 마지막 자유 정착민이면 보류하지 않고 바로 제단 앞으로 보낸다.
        public void HoldPawnUntilReturn(Pawn pawn, int ticks)
        {
            if (pawn == null || pawn.Destroyed) { return; }
            if (_heldPawns.Any(v => v?.pawn == pawn)) { return; }

            // 돌아올 맵: 폰이 있던 맵(보통 보스·앵커 맵). 이미 디스폰된 폰이면 엔딩 맵(앵커 → 보스 → 마지막 앵커 맵) 순으로 정한다.
            var map = pawn.MapHeld ?? AnchorMap ?? _boss?.MapHeld ?? _lastAnchorMap ?? Find.AnyPlayerHomeMap;

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
            // 소멸 연출·찬탈 직후에는 보스 폰이 이미 파괴되어 월드 폰에서 정리될 수 있으므로 참조로 저장하지 않는다.
            // (Boss 단계 감시는 VV_DyingApocrypha가 있는 동안 기다리므로 null이어도 안전하다)
            if (Scribe.mode == LoadSaveMode.Saving && _boss != null && _boss.Destroyed)
            {
                _boss = null;
            }

            // 찬탈·소실로 파괴된 앵커도 같은 이유로 참조를 남기지 않는다(파괴 통지로 이미 Failed가 된 뒤라 null이어도 진행에 영향이 없다).
            if (Scribe.mode == LoadSaveMode.Saving && _anchor != null && _anchor.Destroyed && !IsEndingActive)
            {
                _anchor = null;
            }

            Scribe_Values.Look(ref _stage, "stage", ViviEndingStage.None);
            Scribe_Values.Look(ref _bloomProgress, "bloomProgress", 0f);
            Scribe_References.Look(ref _anchor, "anchor");
            Scribe_References.Look(ref _boss, "boss");

            Scribe_Values.Look(ref _letterTick, "letterTick", -1);
            Scribe_Values.Look(ref _coreDelivered, "coreDelivered", false);
            Scribe_Values.Look(ref _lastCoreDeliveryTick, "lastCoreDeliveryTick", -1);
            Scribe_Values.Look(ref _siegeStartTick, "siegeStartTick", -1);
            Scribe_Values.Look(ref _siegeDurationTicks, "siegeDurationTicks", 0);
            Scribe_Values.Look(ref _nextReinforceTick, "nextReinforceTick", -1);
            Scribe_Values.Look(ref _calmEndTick, "calmEndTick", -1);
            Scribe_Values.Look(ref _bossDeferred, "bossDeferred", false);
            Scribe_Values.Look(ref _bossDeferredMessageSent, "bossDeferredMessageSent", false);
            Scribe_Values.Look(ref _endingEffectTick, "endingEffectTick", -1);
            Scribe_Values.Look(ref _creditsTick, "creditsTick", -1);
            Scribe_References.Look(ref _queen, "queen");
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

        // DEV: 자동 테스트/개발자 기즈모용
        public void DebugSetStage(ViviEndingStage stage)
        {
            DebugSetStage(stage, null);
        }

        public void DebugSetBloom(float value)
        {
            if (_stage != ViviEndingStage.Siege)
            {
                _bloomProgress = Mathf.Clamp01(value);
                return;
            }

            _bloomProgress = Mathf.Clamp(value, 0f, BloomLockedProgress);
            if (_bloomProgress >= BloomLockedProgress)
            {
                StartCalm();
            }
        }

        public void DebugSpawnBossNow()
        {
            DebugSpawnBossNow(null);
        }

        // 각 단계의 진입 함수를 직접 호출한다. context는 앵커가 아직 없을 때 엔딩을 묶을 영원꽃.
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

                case ViviEndingStage.AltarUnlocked:
                    SendFederationLetter(context ?? _anchor);
                    break;

                case ViviEndingStage.Siege:
                    {
                        var everflower = _anchor != null && !_anchor.Destroyed ? _anchor : context ?? FindEndingCandidate();
                        if (everflower == null)
                        {
                            Log.Warning("[ViViRace] ending debug: no everflower to start the siege with.");
                            break;
                        }

                        StartSiege(everflower, DebugRitualQuality);
                    }
                    break;

                case ViviEndingStage.Calm:
                    if (EnsureAnchorForDebug(context))
                    {
                        StartCalm();
                    }
                    break;

                case ViviEndingStage.Boss:
                    DebugSpawnBossNow(context);
                    break;

                case ViviEndingStage.Coronation:
                    if (EnsureAnchorForDebug(context))
                    {
                        // 살아 있는 보스는 토벌된 것으로 치고 호위 말벌과 함께 사라지게 한다.
                        var boss = _boss;
                        _boss = null;
                        VanishBoss(boss);
                        EnterCoronation();
                    }
                    break;

                case ViviEndingStage.Completed:
                    if (EnsureAnchorForDebug(context))
                    {
                        CompleteEnding(removeBoss: true, AnchorResonator);
                    }
                    break;

                case ViviEndingStage.Failed:
                    EnsureAnchorForDebug(context);
                    FailEnding(EndingFailReason.Stolen, removeBoss: true);
                    break;
            }
        }

        internal void DebugSpawnBossNow(ArcanePlant_Everflower context)
        {
            if (IsBossAlive)
            {
                Messages.Message("DEV: Apocrypha is already on the map.", _boss, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (!EnsureAnchorForDebug(context)) { return; }

            if (IsApocryphaDissolving(AnchorMap))
            {
                Messages.Message("DEV: Apocrypha is still dissolving.", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // 보스 단계의 전제(앵커 고정, 응축 99%)를 맞춘 뒤 외곽 진입을 시도한다. 진입 칸이 없으면 개발용으로 아무 외곽 칸이나 쓴다.
            // 보스 없이 Boss 단계에 머무는 일이 없도록 Calm에서 시작한다(등장에 실패하면 Calm·재시도 상태로 남는다).
            if (_stage != ViviEndingStage.Calm)
            {
                _stage = ViviEndingStage.Calm;
                _calmEndTick = Find.TickManager.TicksGame;
                _boss = null;
            }

            _bloomProgress = BloomLockedProgress;
            TryStartBoss(forceSpawn: true);
        }

        private void DebugEndCalm()
        {
            if (_stage != ViviEndingStage.Calm) { return; }

            _calmEndTick = Find.TickManager.TicksGame;
            TryStartBoss(forceSpawn: false);
        }

        private bool EnsureAnchorForDebug(ArcanePlant_Everflower context)
        {
            if (_anchor != null && !_anchor.Destroyed) { return true; }

            var everflower = context ?? FindEndingCandidate();
            if (everflower == null || everflower.Destroyed)
            {
                Log.Warning("[ViViRace] ending debug: no everflower to anchor the ending to.");
                return false;
            }

            _anchor = everflower;
            if (everflower.Spawned)
            {
                _lastAnchorMap = everflower.Map;
            }

            return true;
        }

        private void DebugReset()
        {
            CleanupGimmicks();
            VanishBoss(_boss);
            EndOminousSilence();
            EndGoldenBloom();

            _stage = ViviEndingStage.None;
            _bloomProgress = 0f;
            _anchor = null;
            _boss = null;
            _letterTick = -1;
            _coreDelivered = false;
            _lastCoreDeliveryTick = -1;
            _siegeStartTick = -1;
            _siegeDurationTicks = 0;
            _nextReinforceTick = -1;
            _calmEndTick = -1;
            _bossDeferred = false;
            _bossDeferredMessageSent = false;
            _endingEffectTick = -1;
            _creditsTick = -1;
            _queen = null;
            _bossMissingSinceTick = -1;
            _lastAnchorMap = null;
        }

        // 진행 순서상 다음 단계(개발자 기즈모용). 단계 값은 저장 호환 때문에 진행 순서와 다르다.
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

        // 앵커가 사라졌으면(파괴·맵 제거) 엔딩을 실패로 돌린다. 살아 있으면 true.
        private bool CheckAnchorAlive()
        {
            if (_anchor != null && !_anchor.Destroyed)
            {
                if (_anchor.Spawned)
                {
                    _lastAnchorMap = _anchor.Map;
                }
                return true;
            }

            FailEnding(EndingFailReason.AnchorLost, removeBoss: true);
            return false;
        }

        // {PAWN_labelShort}을 공명자로 채운다. 공명자가 없으면 그 토큰만 대체 문구로 바꾼 뒤 나머지 토큰과 조사를 처리한다.
        private static TaggedString TranslateWithResonator(string key, Pawn resonator, params NamedArgument[] args)
        {
            if (resonator != null)
            {
                var allArgs = new NamedArgument[args.Length + 1];
                allArgs[0] = resonator.Named("PAWN");
                args.CopyTo(allArgs, 1);
                return key.Translate(allArgs);
            }

            var raw = key.Translate().RawText.Replace("{PAWN_labelShort}", LocalizeString_EndingCore.VV_Ending_ResonatorFallback.Translate().RawText);
            return raw.Formatted(args);
        }
    }
}
