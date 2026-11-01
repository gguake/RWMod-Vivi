using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class QuestPart_ViviApocrypha : QuestPartActivable
    {
        private const int RetryIntervalTicks = 2500;
        private const int WatchIntervalTicks = 250;
        private const float BossPointsFactor = 1.5f;

        // 보스 소실 후 통지가 없을 때의 토벌 판정 유예. 7.5초인 소멸 연출보다 길게 둔다.
        private const int BossMissingGraceTicks = 1500;

        public ArcanePlant_Everflower anchor;
        public string inSignalDissolved;
        public string outSignalManifesting;
        public string outSignalArrived;

        private Pawn _boss;

        // 소멸 연출 중 저장하면 파괴된 보스 참조가 null이 되므로 등장 여부를 별도 저장해 중복 소환을 막는다.
        private bool _arrived;
        private bool _deferred;
        private int _nextAttemptTick = -1;
        private int _bossMissingSinceTick = -1;

        private int _manifestSpawnTick = -1;
        private IntVec3 _manifestCell = IntVec3.Invalid;
        private ApocryphaManifestation _manifestation;

        private bool _manifestAnnounced;

        public Pawn Boss => _boss;

        public bool IsDeferred => _deferred;

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

        // 개발자 도구가 같은 호출에서 토벌로 넘길 수 있도록 첫 등장 시도는 다음 틱으로 미룬다.
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

            BeginManifestation(map, entryCell, skipIntro);
            return FinishManifestationIfDue();
        }

        private bool TryFindSpawnCell(Map map, bool forceSpawn, out IntVec3 cell)
        {
            var altar = ViviEndingUtility.AltarOf(anchor);
            var target = altar != null && altar.Spawned && altar.Map == map ? (Thing)altar : anchor;
            return ApocryphaUtility.TryFindEntryCell(map, target.PositionHeld, out cell)
                || ApocryphaUtility.TryFindBreachEntryCell(map, target, out cell)
                || (forceSpawn && TryFindAnyEdgeCell(map, out cell));
        }

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

                // 호위·연출 단계에서 실패했어도 보스가 스폰됐다면 해당 개체로 전투를 시작해 중복 소환을 막는다.
                // 보스 생성이나 스폰 자체가 실패한 경우에만 등장을 미루고 재시도한다.
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

        private static bool TryFindAnyEdgeCell(Map map, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Ignore, out cell);
        }

        private static List<Pawn> SpawnedBosses(Map map)
        {
            var bossDef = ApocryphaThingDefOf.VV_Apocrypha;
            return bossDef != null ? map.listerThings.ThingsOfDef(bossDef).OfType<Pawn>().ToList() : new List<Pawn>();
        }

        private void WatchBoss(int now)
        {
            if (IsBossAlive)
            {
                _bossMissingSinceTick = -1;
                return;
            }

            // 저장·불러오기 후 보스 참조가 null일 수 있으므로 모든 맵에서 진행 중인 소멸 연출을 확인한다.
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

        public void DebugRetryNow()
        {
            if (State != QuestPartState.Enabled || _arrived || IsManifesting) { return; }

            _nextAttemptTick = Find.TickManager.TicksGame;
        }

        public void DebugDefeat()
        {
            if (State != QuestPartState.Enabled) { return; }

            var boss = _boss;
            _boss = null;
            ViviEndingUtility.RunStep("debug: vanish boss", () => ViviEndingUtility.VanishBoss(boss));
            Defeat();
        }

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
