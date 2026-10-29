using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 엔딩 퀘스트 부품(QuestPart)과 창구(ViviEndingQuest)가 함께 쓰는 도우미.
    public static class ViviEndingUtility
    {
        // 엔딩(대관식 완료)이 끝나면 황금빛 꽃안개는 이 시간 동안 남았다가 걷힌다.
        private const int GoldenBloomFadeTicks = 30000;

        private static readonly FieldInfo QueuedIncidentsField = typeof(IncidentQueue).GetField("queuedIncidents", BindingFlags.Instance | BindingFlags.NonPublic);

        // 엔딩을 묶을 후보 영원꽃: 플레이어 소유 중 조율 단계가 가장 높은 것. 같으면 제단 안치·정착지 맵·공명자 연결 순으로 우선한다.
        public static ArcanePlant_Everflower FindEndingCandidate()
        {
            var everflowerDef = VVThingDefOf.VV_Everflower;
            if (everflowerDef == null) { return null; }

            ArcanePlant_Everflower best = null;
            var bestScore = int.MinValue;
            foreach (var map in Find.Maps)
            {
                var things = map.listerThings.ThingsOfDef(everflowerDef);
                for (int i = 0; i < things.Count; ++i)
                {
                    if (!(things[i] is ArcanePlant_Everflower everflower) || everflower.Faction != Faction.OfPlayer) { continue; }

                    var comp = everflower.EverflowerComp;
                    if (comp == null) { continue; }

                    var score = comp.AttunementLevel * 100
                        + (comp.IsEnshrined ? 20 : 0)
                        + (map.IsPlayerHome ? 10 : 0)
                        + (comp.LinkedPawns.Any() ? 1 : 0);

                    if (score > bestScore)
                    {
                        best = everflower;
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        // 패망하지 않은 비비 연방(비적대 우선). requireNonHostile이면 적대 연방밖에 없을 때 null.
        public static Faction FindFederation(bool requireNonHostile)
        {
            var federationDef = EndingCoreDefOf.VV_ViviFederation;
            if (federationDef == null) { return null; }

            Faction hostile = null;
            var factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; ++i)
            {
                var faction = factions[i];
                if (faction.def != federationDef || faction.defeated) { continue; }

                if (!faction.HostileTo(Faction.OfPlayer))
                {
                    return faction;
                }

                if (hostile == null)
                {
                    hostile = faction;
                }
            }

            return requireNonHostile ? null : hostile;
        }

        // 제단이 해금되면 연방 정착지의 기존 재고를 버려, 다음 방문 때 호박 코어 재고(StockGenerator_ViviEndingRelic)가 반영되게 한다.
        public static void RefreshFederationTraderStock()
        {
            var federationDef = EndingCoreDefOf.VV_ViviFederation;
            if (federationDef == null) { return; }

            foreach (var settlement in Find.WorldObjects.Settlements)
            {
                if (settlement.Faction?.def == federationDef)
                {
                    settlement.trader?.TryDestroyStock();
                }
            }
        }

        // 영원꽃의 공명자(첫 번째 연결 폰).
        public static Pawn ResonatorOf(ArcanePlant_Everflower everflower)
        {
            return everflower?.EverflowerComp?.LinkedPawns?.FirstOrDefault();
        }

        // 영원꽃이 안치된 제단(제단 밖으로 밀려났으면 영원꽃 맵의 제단).
        public static Building_DreamumAltar AltarOf(ArcanePlant_Everflower everflower)
        {
            var altar = everflower != null && !everflower.Destroyed ? everflower.EverflowerComp?.EnshrinedAltar : null;
            if (altar != null) { return altar; }

            var map = everflower?.MapHeld;
            return map != null ? Building_DreamumAltar.FindOnMap(map) : null;
        }

        // {PAWN_labelShort}을 공명자로 채운다. 공명자가 없으면 그 토큰만 대체 문구로 바꾼 뒤 나머지 토큰과 조사를 처리한다.
        public static TaggedString TranslateWithResonator(string key, Pawn resonator, params NamedArgument[] args)
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

        // 평화 기간·보스전에 막는 위협 분류: 큰·작은 위협과 심층 드릴 감염(바닐라는 별도 분류라 따로 넣는다).
        public static bool IsThreatCategory(IncidentCategoryDef category)
        {
            return category != null
                && (category == IncidentCategoryDefOf.ThreatBig || category == IncidentCategoryDefOf.ThreatSmall || category == IncidentCategoryDefOf.DeepDrillInfestation);
        }

        // 해당 맵을 노리고 대기열에 올라 있던 위협 사건(마나 파동이 예약한 습격 등)을 지운다.
        public static int PurgeQueuedThreats(Map map)
        {
            if (QueuedIncidentsField == null)
            {
                Log.ErrorOnce("[ViViRace] cannot find IncidentQueue.queuedIncidents; queued threats are not purged during the ending calm.", 0x5645_4E10);
                return 0;
            }

            if (!(QueuedIncidentsField.GetValue(Find.Storyteller.incidentQueue) is List<QueuedIncident> queued)) { return 0; }

            return queued.RemoveAll(v => v?.FiringIncident?.parms?.target == map && IsThreatCategory(v.FiringIncident.def?.category));
        }

        // 맵의 거대 말벌(아포크리파 호위 제외, 플레이어 소유 제외)은 그림자 속으로 사라진다.
        public static int VanishWildTitanicHornets(Map map)
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

        // 맵에서 공격 중인 적대 습격대(인간형·메카노이드)는 원래 로드에서 빠져 맵 밖으로 철수한다.
        // 아직 착륙 중인 드롭포드에 탄 습격대원(스폰 전이지만 이미 습격 로드 소속)도 함께 옮겨, 포드가 열리면 곧바로 철수하게 한다.
        public static int MakeRaidersRetreat(Map map)
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
                    if (pawn.Dead || pawn.Downed || (pawn.Spawned && pawn.Map != map)) { continue; }

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

        // 평화 기간 〈불길한 정적〉을 모든 맵에서 끝낸다(보스 등장·보류·엔딩 종료 시).
        public static void EndOminousSilence()
        {
            foreach (var map in Find.Maps)
            {
                EndOminousSilence(map);
            }
        }

        // 한 맵의 〈불길한 정적〉을 끝낸다(앵커가 다른 맵으로 옮겨졌을 때 등).
        public static void EndOminousSilence(Map map)
        {
            var conditionDef = EndingCoreDefOf.VV_OminousSilence;
            if (conditionDef == null || map == null) { return; }

            map.gameConditionManager.GetActiveCondition(conditionDef)?.End();
        }

        // 엔딩 완료: 엔딩이 건 꽃안개는 잠시 남았다가 걷힌다(이후로는 의식으로 부른 꽃안개처럼 다룬다).
        // 맵의 첫 꽃안개가 엔딩 꽃안개가 아닐 수 있으므로(의식으로 부른 꽃안개 등) 첫 번째만 보지 않고 모두 확인한다.
        public static void FadeGoldenBloom()
        {
            foreach (var map in Find.Maps)
            {
                foreach (var condition in EndingGoldenBlooms(map))
                {
                    condition.fromEnding = false;
                    condition.Permanent = false;
                    condition.Duration = condition.TicksPassed + GoldenBloomFadeTicks;
                }
            }
        }

        // 엔딩 실패·초기화: 엔딩이 건 꽃안개를 곧바로 걷는다.
        public static void EndGoldenBloom()
        {
            foreach (var map in Find.Maps)
            {
                EndGoldenBloom(map);
            }
        }

        // 한 맵에서 엔딩이 건 꽃안개를 곧바로 걷는다(앵커가 다른 맵으로 옮겨졌을 때 등).
        public static void EndGoldenBloom(Map map)
        {
            foreach (var condition in EndingGoldenBlooms(map))
            {
                condition.End();
            }
        }

        // 맵에 걸린 꽃안개 중 엔딩이 건 것(걷는 동안 목록이 바뀌므로 복사본).
        private static List<GameCondition_GoldenBloom> EndingGoldenBlooms(Map map)
        {
            var result = new List<GameCondition_GoldenBloom>();
            var conditions = map?.gameConditionManager.ActiveConditions;
            if (conditions == null) { return result; }

            for (int i = 0; i < conditions.Count; ++i)
            {
                if (conditions[i] is GameCondition_GoldenBloom condition && condition.fromEnding)
                {
                    result.Add(condition);
                }
            }

            return result;
        }

        // 멸망꽃·요정꽃·요정 간섭·멸망꽃 페널티를 모든 맵에서 정리한다(앵커가 이미 사라졌어도 빠짐없이).
        public static void CleanupGimmicks()
        {
            foreach (var map in Find.Maps.ToList())
            {
                try
                {
                    ApocryphaGimmicks.CleanupAll(map);
                }
                catch (Exception ex)
                {
                    Log.Error($"[ViViRace] failed to clean up Apocrypha gimmicks on {map}: {ex}");
                }
            }
        }

        // 살아 있는 보스를 호위 거대 말벌과 함께 순간 소실 연출로 없앤다(시체 없음). 엔딩이 보스 생존 중에 끝났을 때만 쓴다.
        public static void VanishBoss(Pawn boss)
        {
            if (boss == null || boss.Destroyed || !boss.Spawned) { return; }

            var map = boss.Map;
            var vanishEffect = ApocryphaEffecterDefOf.VV_Effecter_ApocryphaVanish ?? EffecterDefOf.Skip_EntryNoDelay;
            foreach (var hornet in ApocryphaUtility.GetEscorts(boss))
            {
                if (hornet == null || hornet.Destroyed) { continue; }

                if (hornet.Spawned)
                {
                    vanishEffect.Spawn(hornet.Position, hornet.Map).Cleanup();
                }

                hornet.Destroy(DestroyMode.Vanish);
            }

            vanishEffect.Spawn(boss.Position, map, 2.5f).Cleanup();
            boss.Destroy(DestroyMode.Vanish);
        }

        // 여러 단계를 잇는 처리에서 한 단계가 실패해도 나머지는 이어서 진행한다.
        public static void RunStep(string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error($"[ViViRace] ending step '{label}' failed: {ex}");
            }
        }
    }
}
