using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 【막간】 〈폭풍 전의 고요〉 평화 기간과 위협 억제, 【2막】 아포크리파 등장과 보스 단계 감시
    public partial class ViviEndingQuest
    {
        private static readonly IntRange CalmDurationTicks = new IntRange(90000, 120000);
        private const int BossRetryIntervalTicks = 2500;
        private const float BossPointsFactor = 1.5f;

        // 보스 폰이 사라진 뒤 아무 알림도 없을 때 엔딩으로 넘기기까지 기다리는 시간(소멸 연출 7.5초보다 넉넉하게).
        private const int BossMissingGraceTicks = 1500;

        private static readonly FieldInfo QueuedIncidentsField = typeof(IncidentQueue).GetField("queuedIncidents", BindingFlags.Instance | BindingFlags.NonPublic);

        private void StartCalm()
        {
            var now = Find.TickManager.TicksGame;
            var duration = CalmDurationTicks.RandomInRange;

            _stage = ViviEndingStage.Calm;
            _bloomProgress = BloomLockedProgress;
            _calmEndTick = now + duration;
            _bossDeferred = false;
            _bossDeferredMessageSent = false;

            var map = AnchorMap;
            if (map != null)
            {
                RegisterOminousSilence(map, duration);
                PurgeQueuedThreats(map);

                // 설계서: "모든 일반 습격이 즉시 중단", "거대 말벌 무리조차 그림자 속으로 자취를 감추었습니다".
                RunEndingStep("calm: hornets vanish", () => VanishWildTitanicHornets(map));
                RunEndingStep("calm: raiders retreat", () => MakeRaidersRetreat(map));
            }

            var days = (duration / (float)GenDate.TicksPerDay).ToString("0.#");
            var text = TranslateWithResonator(LocalizeString_EndingCore.VV_Letter_EndingCalm, AnchorResonator).Resolve()
                + "\n\n" + LocalizeString_EndingCore.VV_Letter_EndingCalmGuide.Translate(days.Named("DAYS")).Resolve();

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingCore.VV_Letter_EndingCalmLabel.Translate(),
                text,
                LetterDefOf.NeutralEvent,
                _anchor != null && _anchor.Spawned ? new LookTargets(_anchor) : LookTargets.Invalid);
        }

        // 250틱마다: 평화가 끝나면 보스 등장 시도. 외곽 등장이 불가능해 미뤄졌으면 2500틱마다 다시 시도한다.
        private void TickCalm(int now)
        {
            if (!CheckAnchorAlive()) { return; }

            if (_bossDeferred)
            {
                if (now % BossRetryIntervalTicks == 0)
                {
                    TryStartBoss(forceSpawn: false);
                }
                return;
            }

            if (_calmEndTick < 0)
            {
                _calmEndTick = now;
            }

            if (now >= _calmEndTick)
            {
                TryStartBoss(forceSpawn: false);
            }
        }

        // 맵 외곽에서 아포크리파와 호위 말벌을 등장시킨다. 실패하면 위협 억제를 풀고 Calm에 머문 채 재시도를 기다린다.
        private bool TryStartBoss(bool forceSpawn)
        {
            var map = AnchorMap;
            if (map == null || _anchor == null || _anchor.Destroyed) { return false; }

            var altar = AnchorAltar;
            var target = altar != null && altar.Spawned && altar.Map == map ? altar.Position : _anchor.Position;

            Pawn boss = null;
            if (ApocryphaUtility.TryFindEntryCell(map, target, out var entryCell) || (forceSpawn && TryFindAnyEdgeCell(map, out entryCell)))
            {
                try
                {
                    boss = ApocryphaUtility.SpawnApocrypha(map, entryCell, StorytellerUtility.DefaultThreatPointsNow(map) * BossPointsFactor);
                }
                catch (System.Exception ex)
                {
                    // 예외로 생성·스폰에 실패해도 보스 없이 Boss 단계에 들어가지 않도록 등장을 미루고 재시도한다.
                    Log.Error($"[ViViRace] failed to spawn Apocrypha: {ex}");
                    boss = null;
                }
            }

            if (boss == null)
            {
                _bossDeferred = true;
                EndOminousSilence();

                if (!_bossDeferredMessageSent)
                {
                    _bossDeferredMessageSent = true;
                    Messages.Message(
                        LocalizeString_EndingCore.VV_Message_EndingApocryphaDeferred.Translate(),
                        new LookTargets(_anchor),
                        MessageTypeDefOf.NeutralEvent);
                }

                return false;
            }

            _boss = boss;
            _stage = ViviEndingStage.Boss;
            _bloomProgress = BloomLockedProgress;
            _bossDeferred = false;
            _bossDeferredMessageSent = false;
            _bossMissingSinceTick = -1;
            EndOminousSilence();

            Find.LetterStack.ReceiveLetter(
                LocalizeString_EndingCore.VV_Letter_EndingApocryphaLabel.Translate(),
                LocalizeString_EndingCore.VV_Letter_EndingApocrypha.Translate(),
                LetterDefOf.ThreatBig,
                new LookTargets(boss));

            return true;
        }

        // 개발자 기즈모 전용: 정식 진입 칸을 못 찾아도 설 수 있는 아무 외곽 칸에 등장시킨다.
        private static bool TryFindAnyEdgeCell(Map map, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Ignore, out cell);
        }

        // 250틱마다: 응축 99% 고정. 보스가 아무 알림 없이 사라졌으면(개발자 도구 등) 경고 후 대관식 단계로 넘겨 진행이 막히지 않게 한다.
        private void TickBoss(int now)
        {
            if (!CheckAnchorAlive()) { return; }

            _bloomProgress = BloomLockedProgress;

            if (IsBossAlive && _boss.Map == AnchorMap)
            {
                _bossMissingSinceTick = -1;
                return;
            }

            // 소멸 연출 중이면(보스 폰은 즉시 파괴되고 VV_DyingApocrypha가 450틱 뒤 Notify_BossDefeated를 보낸다) 기다린다.
            if (BossComp?.Phase == ApocryphaPhase.Dying || IsApocryphaDissolving(AnchorMap) || IsApocryphaDissolving(_lastAnchorMap))
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
            EnterCoronation();
        }

        private static bool IsApocryphaDissolving(Map map)
        {
            return map != null && ApocryphaUtility.IsDefeatInProgress(map);
        }

        private static void RegisterOminousSilence(Map map, int duration)
        {
            var conditionDef = EndingCoreDefOf.VV_OminousSilence;
            if (conditionDef == null) { return; }

            var existing = map.gameConditionManager.GetActiveCondition(conditionDef);
            if (existing != null)
            {
                existing.TicksLeft = duration;
                return;
            }

            map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(conditionDef, duration));
        }

        private static void EndOminousSilence()
        {
            var conditionDef = EndingCoreDefOf.VV_OminousSilence;
            if (conditionDef == null) { return; }

            foreach (var map in Find.Maps)
            {
                map.gameConditionManager.GetActiveCondition(conditionDef)?.End();
            }
        }

        // 평화가 시작되면 해당 맵을 노리고 대기열에 올라 있던 위협 사건(마나 파동이 예약한 습격 등)을 지운다.
        private static int PurgeQueuedThreats(Map map)
        {
            if (QueuedIncidentsField == null)
            {
                Log.ErrorOnce("[ViViRace] cannot find IncidentQueue.queuedIncidents; queued threats are not purged during the ending calm.", 0x5645_4E10);
                return 0;
            }

            if (!(QueuedIncidentsField.GetValue(Find.Storyteller.incidentQueue) is List<QueuedIncident> queued)) { return 0; }

            return queued.RemoveAll(v => v?.FiringIncident?.parms?.target == map && IsThreatCategory(v.FiringIncident.def?.category));
        }

        internal static bool IsThreatCategory(IncidentCategoryDef category)
        {
            return category != null && (category == IncidentCategoryDefOf.ThreatBig || category == IncidentCategoryDefOf.ThreatSmall);
        }

        // 평화가 시작되면 앵커 맵의 거대 말벌(아포크리파 호위 제외, 플레이어 소유 제외)은 그림자 속으로 사라진다.
        private static int VanishWildTitanicHornets(Map map)
        {
            var hornetDef = VVThingDefOf.VV_TitanicHornet;
            if (hornetDef == null) { return 0; }

            var vanished = 0;
            foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.def != hornetDef || pawn.Destroyed || pawn.Faction == Faction.OfPlayer) { continue; }
                if (pawn.GetLord()?.LordJob is LordJob_ApocryphaEscort) { continue; }

                EffecterDefOf.Skip_EntryNoDelay.Spawn(pawn, map).Cleanup();
                pawn.Destroy(DestroyMode.Vanish);
                vanished++;
            }

            return vanished;
        }

        // 앵커 맵에서 공격 중인 적대 습격대(인간형·메카노이드)는 원래 로드에서 빠져 맵 밖으로 철수한다.
        private static int MakeRaidersRetreat(Map map)
        {
            var retreated = 0;
            foreach (var lord in map.lordManager.lords.ToList())
            {
                var faction = lord.faction;
                if (faction == null || faction == Faction.OfPlayer || !faction.HostileTo(Faction.OfPlayer)) { continue; }
                if (!IsAssaultLordJob(lord.LordJob)) { continue; }

                var leavers = new List<Pawn>();
                foreach (var pawn in lord.ownedPawns.ToList())
                {
                    if (!pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map != map) { continue; }

                    lord.Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord);
                    if (pawn.GetLord() == null)
                    {
                        leavers.Add(pawn);
                    }
                }

                if (leavers.Count == 0) { continue; }

                LordMaker.MakeNewLord(faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, canDig: true, canDefendSelf: true), map, leavers);
                retreated += leavers.Count;
            }

            return retreated;
        }

        private static bool IsAssaultLordJob(LordJob lordJob)
        {
            return lordJob is LordJob_AssaultColony
                || lordJob is LordJob_Siege
                || lordJob is LordJob_StageThenAttack
                || lordJob is LordJob_AssaultThings
                || lordJob is LordJob_SleepThenAssaultColony
                || lordJob is LordJob_BossgroupAssaultColony;
        }
    }
}
