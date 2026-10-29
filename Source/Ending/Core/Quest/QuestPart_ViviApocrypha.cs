using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 〈바스러진 여왕, 아포크리파〉: 평화 기간이 끝나면 맵 외곽에서 호위 말벌과 함께 등장시킨다.
    // 외곽에서 제단으로 이어지는 길이 없으면 등장을 미루고(평화 기간 상태로 위협 억제를 풀어 둠) 2500틱마다 다시 시도한다.
    // 등장한 뒤에는 보스를 감시하다가, 소멸 연출이 끝났다는 신호(창구가 보냄)나 아무 알림 없이 사라진 뒤 유예가 지나면 토벌로 처리한다.
    public class QuestPart_ViviApocrypha : QuestPartActivable
    {
        private const int RetryIntervalTicks = 2500;
        private const int WatchIntervalTicks = 250;
        private const float BossPointsFactor = 1.5f;

        // 보스 폰이 사라진 뒤 아무 알림도 없을 때 토벌로 넘기기까지 기다리는 시간(소멸 연출 7.5초보다 넉넉하게).
        private const int BossMissingGraceTicks = 1500;

        public ArcanePlant_Everflower anchor;
        public string inSignalDissolved;
        public string outSignalArrived;

        private Pawn _boss;

        // 한 번이라도 등장했는지. 소멸 연출 중 저장하면 파괴된 보스 참조는 null로 저장되므로, 등장 여부를 따로 기억해 다시 소환하지 않는다.
        private bool _arrived;
        private bool _deferred;
        private bool _deferredMessageSent;
        private int _nextAttemptTick = -1;
        private int _bossMissingSinceTick = -1;

        public Pawn Boss => _boss;

        // 등장할 길을 찾지 못해 미뤄진 상태인지(이 동안에는 평화 기간이 이어지되 위협 억제가 풀린다).
        public bool IsDeferred => _deferred;

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
                if (now >= _nextAttemptTick)
                {
                    TrySpawn(forceSpawn: false);
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

        // 맵 외곽에서 아포크리파와 호위 말벌을 등장시킨다. 실패하면 위협 억제를 풀고 2500틱 뒤 다시 시도한다.
        private bool TrySpawn(bool forceSpawn)
        {
            var map = AnchorMap;
            if (map == null)
            {
                _nextAttemptTick = Find.TickManager.TicksGame + RetryIntervalTicks;
                return false;
            }

            var altar = ViviEndingUtility.AltarOf(anchor);
            var target = altar != null && altar.Spawned && altar.Map == map ? altar.Position : anchor.PositionHeld;

            Pawn boss = null;
            if (ApocryphaUtility.TryFindEntryCell(map, target, out var entryCell) || (forceSpawn && TryFindAnyEdgeCell(map, out entryCell)))
            {
                try
                {
                    boss = ApocryphaUtility.SpawnApocrypha(map, entryCell, StorytellerUtility.DefaultThreatPointsNow(map) * BossPointsFactor);
                }
                catch (Exception ex)
                {
                    // 예외로 생성·스폰에 실패해도 보스 없이 보스 단계에 들어가지 않도록 등장을 미루고 재시도한다.
                    Log.Error($"[ViViRace] failed to spawn Apocrypha: {ex}");
                    boss = null;
                }
            }

            if (boss == null)
            {
                _deferred = true;
                _nextAttemptTick = Find.TickManager.TicksGame + RetryIntervalTicks;
                ViviEndingUtility.EndOminousSilence();

                if (!_deferredMessageSent)
                {
                    _deferredMessageSent = true;
                    Messages.Message(
                        LocalizeString_EndingCore.VV_Message_EndingApocryphaDeferred.Translate(),
                        new LookTargets(anchor),
                        MessageTypeDefOf.NeutralEvent);
                }

                return false;
            }

            _boss = boss;
            _arrived = true;
            _deferred = false;
            _deferredMessageSent = false;
            _bossMissingSinceTick = -1;
            ViviEndingUtility.EndOminousSilence();

            Find.SignalManager.SendSignal(new Signal(outSignalArrived, boss.Named("SUBJECT")));
            return true;
        }

        // 개발자 도구 전용: 정식 진입 칸을 못 찾아도 설 수 있는 아무 외곽 칸에 등장시킨다.
        private static bool TryFindAnyEdgeCell(Map map, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Ignore, out cell);
        }

        // 250틱마다: 보스가 아무 알림 없이 사라졌으면(개발자 도구 등) 경고 후 토벌로 처리해 진행이 막히지 않게 한다.
        private void WatchBoss(int now)
        {
            var map = AnchorMap;
            if (IsBossAlive && _boss.Map == map)
            {
                _bossMissingSinceTick = -1;
                return;
            }

            // 소멸 연출 중이면(보스 폰은 즉시 파괴되고 VV_DyingApocrypha가 450틱 뒤 창구에 알린다) 기다린다.
            if (_boss?.GetComp<CompApocrypha>()?.Phase == ApocryphaPhase.Dying || ApocryphaUtility.IsDefeatInProgress(map) || ApocryphaUtility.IsDefeatInProgress(_boss?.MapHeld))
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

            ViviEndingUtility.RunStep("defeat: cleanup gimmicks", ViviEndingUtility.CleanupGimmicks);
            ViviEndingUtility.RunStep("defeat: end ominous silence", ViviEndingUtility.EndOminousSilence);

            Complete();
        }

        // DEV: 정식 진입 칸이 없어도 등장시킨다. 이미 있거나 소멸 연출 중이면 거절한다.
        public void DebugSpawnNow()
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

            _boss = null;
            TrySpawn(forceSpawn: true);
        }

        // DEV: 등장이 미뤄진 상태면 다음 틱에 곧바로 다시 시도한다.
        public void DebugRetryNow()
        {
            if (State != QuestPartState.Enabled || _arrived) { return; }

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
            Scribe_Values.Look(ref outSignalArrived, "outSignalArrived");
            Scribe_References.Look(ref _boss, "boss");
            Scribe_Values.Look(ref _arrived, "arrived", false);
            Scribe_Values.Look(ref _deferred, "deferred", false);
            Scribe_Values.Look(ref _deferredMessageSent, "deferredMessageSent", false);
            Scribe_Values.Look(ref _nextAttemptTick, "nextAttemptTick", -1);
            Scribe_Values.Look(ref _bossMissingSinceTick, "bossMissingSinceTick", -1);
        }
    }
}
