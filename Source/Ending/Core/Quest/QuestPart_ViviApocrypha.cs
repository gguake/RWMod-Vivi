using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 〈바스러진 여왕, 아포크리파〉: 평화 기간이 끝나면 등장 연출(ApocryphaManifestation)을 거쳐 맵 외곽에서 호위 말벌과 함께 등장시킨다.
    // 등장 칸을 먼저 정한 뒤 영원꽃 위에서 형체가 빚어지고 파장에 밀려나는 연출을 틀고(편지 신호), 형체가 그 칸에 내려앉는 틱에 실제 보스를 스폰한다.
    // 외곽에서 제단으로 이어지는 길이 없으면 등장을 미루고(평화 기간 상태로 위협 억제를 풀어 둠) 2500틱마다 다시 시도한다.
    // 등장한 뒤에는 보스를 감시하다가, 소멸 연출이 끝났다는 신호(창구가 보냄)나 아무 알림 없이 완전히 사라진 뒤(죽음·파괴·어느 맵에도 없음) 유예가 지나면 토벌로 처리한다.
    public class QuestPart_ViviApocrypha : QuestPartActivable
    {
        private const int RetryIntervalTicks = 2500;
        private const int WatchIntervalTicks = 250;
        private const float BossPointsFactor = 1.5f;

        // 보스 폰이 사라진 뒤 아무 알림도 없을 때 토벌로 넘기기까지 기다리는 시간(소멸 연출 7.5초보다 넉넉하게).
        private const int BossMissingGraceTicks = 1500;

        public ArcanePlant_Everflower anchor;
        public string inSignalDissolved;
        public string outSignalManifesting;
        public string outSignalArrived;

        private Pawn _boss;

        // 한 번이라도 등장했는지. 소멸 연출 중 저장하면 파괴된 보스 참조는 null로 저장되므로, 등장 여부를 따로 기억해 다시 소환하지 않는다.
        private bool _arrived;
        private bool _deferred;
        private int _nextAttemptTick = -1;
        private int _bossMissingSinceTick = -1;

        // 등장 연출 중: 실제 보스를 스폰할 틱(-1이면 연출 중이 아님)과 칸, 연출 시퀀서
        private int _manifestSpawnTick = -1;
        private IntVec3 _manifestCell = IntVec3.Invalid;
        private ApocryphaManifestation _manifestation;

        // 등장 편지 신호를 보냈는지(스폰 실패로 연출을 다시 틀어도 편지는 한 번만).
        private bool _manifestAnnounced;

        public Pawn Boss => _boss;

        // 등장할 길을 찾지 못해 미뤄진 상태인지(이 동안에는 평화 기간이 이어지되 위협 억제가 풀린다).
        public bool IsDeferred => _deferred;

        // 등장 연출이 진행 중인지(보스는 아직 없다).
        public bool IsManifesting => _manifestSpawnTick >= 0;

        public bool IsBossAlive => _boss != null && !_boss.Destroyed && !_boss.Dead && _boss.Spawned;

        private Map AnchorMap => anchor != null && !anchor.Destroyed ? anchor.MapHeld : null;

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                foreach (var target in base.QuestLookTargets)
                {
                    yield return target;
                }

                if (IsBossAlive)
                {
                    yield return _boss;
                }
            }
        }

        // 첫 등장 시도는 다음 틱에 한다(개발자 도구가 같은 호출 안에서 토벌로 넘길 수 있도록).
        protected override void Enable(SignalArgs receivedArgs)
        {
            base.Enable(receivedArgs);
            _nextAttemptTick = Find.TickManager.TicksGame;
        }

        public override void QuestPartTick()
        {
            base.QuestPartTick();

            var now = Find.TickManager.TicksGame;
            if (!_arrived)
            {
                if (IsManifesting)
                {
                    if (now >= _manifestSpawnTick)
                    {
                        FinishManifestation();
                    }
                }
                else if (now >= _nextAttemptTick)
                {
                    TrySpawn(forceSpawn: false, skipIntro: false);
                }
                return;
            }

            if (now % WatchIntervalTicks == 0)
            {
                WatchBoss(now);
            }
        }

        protected override void ProcessQuestSignal(Signal signal)
        {
            base.ProcessQuestSignal(signal);

            if (signal.tag == inSignalDissolved && _arrived)
            {
                Defeat();
            }
        }

        // 등장 칸(맵 외곽)을 정하고 영원꽃 위에서 등장 연출을 시작한다(skipIntro면 곧바로 스폰). 칸을 찾지 못하면 위협 억제를 풀고 2500틱 뒤 다시 시도한다.
        private bool TrySpawn(bool forceSpawn, bool skipIntro)
        {
            var map = AnchorMap;
            if (map == null)
            {
                _nextAttemptTick = Find.TickManager.TicksGame + RetryIntervalTicks;
                return false;
            }

            if (!TryFindSpawnCell(map, forceSpawn, out var entryCell))
            {
                Defer();
                return false;
            }

            // 연출이 없으면(skipIntro, 영원꽃이 맵에 없음) 곧바로 스폰한다.
            BeginManifestation(map, entryCell, skipIntro);
            return FinishManifestationIfDue();
        }

        private bool TryFindSpawnCell(Map map, bool forceSpawn, out IntVec3 cell)
        {
            var altar = ViviEndingUtility.AltarOf(anchor);
            var target = altar != null && altar.Spawned && altar.Map == map ? altar.Position : anchor.PositionHeld;
            return ApocryphaUtility.TryFindEntryCell(map, target, out cell) || (forceSpawn && TryFindAnyEdgeCell(map, out cell));
        }

        // 등장 연출 시작: 〈불길한 정적〉을 끝내고 등장 편지 신호(SUBJECT = 영원꽃)를 한 번만 보낸 뒤, 영원꽃 위에서 형체가 빚어지고 밀려나는 연출을 튼다.
        // 영원꽃이 맵에 서 있지 않으면 연출 없이 다음 확인(곧바로)에 스폰한다.
        private void BeginManifestation(Map map, IntVec3 cell, bool skipIntro)
        {
            _manifestCell = cell;
            _manifestSpawnTick = Find.TickManager.TicksGame;
            _deferred = false;
            ViviEndingUtility.EndOminousSilence();

            if (!skipIntro && anchor.Spawned && anchor.Map == map)
            {
                _manifestation = ApocryphaManifestation.Begin(anchor, cell, ApocryphaUtility.EntryRotation(map, cell));
                if (_manifestation != null)
                {
                    _manifestSpawnTick = _manifestation.SpawnTick;
                }
            }

            Find.TickManager.slower.SignalForceNormalSpeedShort();

            if (!_manifestAnnounced && !outSignalManifesting.NullOrEmpty())
            {
                _manifestAnnounced = true;
                Find.SignalManager.SendSignal(new Signal(outSignalManifesting, anchor.Named("SUBJECT")));
            }
        }

        private bool FinishManifestationIfDue()
        {
            return Find.TickManager.TicksGame >= _manifestSpawnTick && FinishManifestation();
        }

        // 형체가 외곽에 내려앉는 틱: 연출을 지우고 그 칸에 실제 아포크리파와 호위 말벌을 스폰한다.
        // 연출 동안 그 칸에 설 수 없게 되었으면 다시 찾고(없으면 아무 외곽 칸), 앵커 맵을 잃었거나 스폰에 실패하면 등장을 미룬다.
        private bool FinishManifestation()
        {
            var cell = _manifestCell;
            if (_manifestation != null && !_manifestation.Destroyed)
            {
                _manifestation.Land();
            }
            ClearManifestation();

            var map = AnchorMap;
            if (map == null)
            {
                _nextAttemptTick = Find.TickManager.TicksGame + RetryIntervalTicks;
                return false;
            }

            if ((!cell.IsValid || !cell.InBounds(map) || !cell.Standable(map)) && !TryFindSpawnCell(map, forceSpawn: true, out cell))
            {
                Defer();
                return false;
            }

            var bossesBefore = SpawnedBosses(map);
            Pawn boss;
            try
            {
                boss = ApocryphaUtility.SpawnApocrypha(map, cell, StorytellerUtility.DefaultThreatPointsNow(map) * BossPointsFactor);
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] failed to spawn Apocrypha: {ex}");

                // 보스 폰이 이미 스폰된 뒤의 단계(호위 말벌·연출)에서 실패했다면 그 보스로 보스 단계를 시작한다(재시도가 보스를 또 소환하지 않게).
                // 생성·스폰 자체가 실패했으면 보스 없이 보스 단계에 들어가지 않도록 등장을 미루고 재시도한다.
                boss = SpawnedBosses(map).FirstOrDefault(v => !bossesBefore.Contains(v) && !v.Dead && !v.Destroyed);
            }

            if (boss == null)
            {
                Defer();
                return false;
            }

            _boss = boss;
            _arrived = true;
            _deferred = false;
            _bossMissingSinceTick = -1;
            ViviEndingUtility.EndOminousSilence();

            Messages.Message(LocalizeString_EndingCore.VV_Message_EndingApocryphaLanded.Translate(), boss, MessageTypeDefOf.ThreatBig);
            Find.SignalManager.SendSignal(new Signal(outSignalArrived, boss.Named("SUBJECT")));
            return true;
        }

        // 등장을 미룬다: 위협 억제를 풀고 2500틱 뒤 다시 시도한다. 처음 미뤄질 때만 알린다(등장하면 _deferred가 풀린다).
        private void Defer()
        {
            _nextAttemptTick = Find.TickManager.TicksGame + RetryIntervalTicks;
            ViviEndingUtility.EndOminousSilence();

            var firstDeferral = !_deferred;
            _deferred = true;
            if (firstDeferral)
            {
                Messages.Message(
                    LocalizeString_EndingCore.VV_Message_EndingApocryphaDeferred.Translate(),
                    new LookTargets(anchor),
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        // 진행 중인 등장 연출을 멈추고 지운다.
        private void ClearManifestation()
        {
            _manifestSpawnTick = -1;
            _manifestCell = IntVec3.Invalid;
            if (_manifestation != null && !_manifestation.Destroyed)
            {
                _manifestation.Destroy();
            }
            _manifestation = null;
        }

        // 개발자 도구 전용: 정식 진입 칸을 못 찾아도 설 수 있는 아무 외곽 칸에 등장시킨다.
        private static bool TryFindAnyEdgeCell(Map map, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Ignore, out cell);
        }

        private static List<Pawn> SpawnedBosses(Map map)
        {
            var bossDef = ApocryphaThingDefOf.VV_Apocrypha;
            return bossDef != null ? map.listerThings.ThingsOfDef(bossDef).OfType<Pawn>().ToList() : new List<Pawn>();
        }

        // 250틱마다: 보스가 아무 알림 없이 완전히 사라졌으면(개발자 도구·다른 모드의 소멸 등) 경고 후 토벌로 처리해 진행이 막히지 않게 한다.
        // 살아서 어느 맵에든 있으면(앵커가 중력선으로 다른 맵에 옮겨진 경우 등) 토벌로 치지 않고 계속 기다린다.
        private void WatchBoss(int now)
        {
            if (IsBossAlive)
            {
                _bossMissingSinceTick = -1;
                return;
            }

            // 소멸 연출 중이면(보스 폰은 즉시 파괴되고 VV_DyingApocrypha가 450틱 뒤 창구에 알린다) 기다린다.
            // 저장·불러오기를 거치면 파괴된 보스 참조가 null이 되므로 연출이 진행 중인 맵을 모두 확인한다.
            if (_boss?.GetComp<CompApocrypha>()?.Phase == ApocryphaPhase.Dying || Find.Maps.Any(ApocryphaUtility.IsDefeatInProgress))
            {
                _bossMissingSinceTick = -1;
                return;
            }

            if (_bossMissingSinceTick < 0)
            {
                _bossMissingSinceTick = now;
                return;
            }

            if (now - _bossMissingSinceTick < BossMissingGraceTicks) { return; }

            Log.Warning("[ViViRace] Apocrypha disappeared during the boss stage without notifying the ending quest; unlocking the coronation.");
            Defeat();
        }

        // 토벌: 기믹과 〈불길한 정적〉을 정리하고 완료 신호(→ 대관식 대기)를 보낸다.
        private void Defeat()
        {
            _boss = null;
            _deferred = false;
            _bossMissingSinceTick = -1;
            ClearManifestation();

            ViviEndingUtility.RunStep("defeat: cleanup gimmicks", ViviEndingUtility.CleanupGimmicks);
            ViviEndingUtility.RunStep("defeat: end ominous silence", ViviEndingUtility.EndOminousSilence);

            Complete();
        }

        // DEV: 정식 진입 칸이 없어도 등장시킨다. 이미 있거나 소멸 연출 중이면 거절한다.
        // skipIntro면 등장 연출 없이 곧바로 스폰한다(단계 이동·자동 테스트). 등장 연출 중이면 연출을 건너뛰고 곧바로 스폰한다.
        public void DebugSpawnNow(bool skipIntro)
        {
            if (State != QuestPartState.Enabled) { return; }

            if (IsBossAlive)
            {
                Messages.Message("DEV: Apocrypha is already on the map.", _boss, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (ApocryphaUtility.IsDefeatInProgress(AnchorMap))
            {
                Messages.Message("DEV: Apocrypha is still dissolving.", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (IsManifesting)
            {
                FinishManifestation();
                return;
            }

            _boss = null;
            TrySpawn(forceSpawn: true, skipIntro);
        }

        // DEV: 등장이 미뤄진 상태면 다음 틱에 곧바로 다시 시도한다.
        public void DebugRetryNow()
        {
            if (State != QuestPartState.Enabled || _arrived || IsManifesting) { return; }

            _nextAttemptTick = Find.TickManager.TicksGame;
        }

        // DEV: 살아 있는 보스는 토벌된 것으로 치고 호위 말벌과 함께 사라지게 한 뒤 대관식 대기로 넘긴다.
        public void DebugDefeat()
        {
            if (State != QuestPartState.Enabled) { return; }

            var boss = _boss;
            _boss = null;
            ViviEndingUtility.RunStep("debug: vanish boss", () => ViviEndingUtility.VanishBoss(boss));
            Defeat();
        }

        // 퀘스트가 끝날 때: 실패(찬탈 제외)·개발자 도구로 끝났는데 보스가 살아 있으면 호위 말벌과 함께 사라지게 한다.
        // 찬탈은 보스 모듈이 이 정리 뒤에 영원꽃·호위·보스를 직접 지운다.
        public override void Cleanup()
        {
            base.Cleanup();
            ClearManifestation();

            if (quest.State == QuestState.EndedSuccess || !IsBossAlive) { return; }

            var state = quest.GetFirstPartOfType<QuestPart_ViviEndingState>();
            if (state != null && state.Stolen) { return; }

            var boss = _boss;
            _boss = null;
            ViviEndingUtility.RunStep("cleanup: vanish boss", () => ViviEndingUtility.VanishBoss(boss));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref inSignalDissolved, "inSignalDissolved");
            Scribe_Values.Look(ref outSignalManifesting, "outSignalManifesting");
            Scribe_Values.Look(ref outSignalArrived, "outSignalArrived");
            Scribe_References.Look(ref _boss, "boss");
            Scribe_Values.Look(ref _arrived, "arrived", false);
            Scribe_Values.Look(ref _deferred, "deferred", false);
            Scribe_Values.Look(ref _nextAttemptTick, "nextAttemptTick", -1);
            Scribe_Values.Look(ref _bossMissingSinceTick, "bossMissingSinceTick", -1);
            Scribe_Values.Look(ref _manifestSpawnTick, "manifestSpawnTick", -1);
            Scribe_Values.Look(ref _manifestCell, "manifestCell", IntVec3.Invalid);
            Scribe_References.Look(ref _manifestation, "manifestation");
            Scribe_Values.Look(ref _manifestAnnounced, "manifestAnnounced", false);
        }
    }
}
