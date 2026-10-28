using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // [계약] 아포크리파 출현/호위 말벌 관련 정적 API.
    public static class ApocryphaUtility
    {
        private static readonly List<Pawn> _tmpEscorts = new List<Pawn>();

        public static bool IsApocrypha(Pawn pawn)
        {
            return pawn != null && pawn.def == ApocryphaThingDefOf.VV_Apocrypha;
        }

        // 맵 외곽에서 target(제단)까지 걸어 들어올 수 있는 진입 칸을 찾는다. 우주/궤도/포켓 맵 등 외곽 등장이 불가능하면 false.
        public static bool TryFindEntryCell(Map map, IntVec3 target, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (!CanEnterFromEdge(map)) { return false; }

            // 2페이즈 돌진처럼 구조물을 부수며 진입하므로, 파괴 가능한 장애물은 모두 통과할 수 있다고 보고 도달 가능성을 판단한다.
            var reachParms = TraverseParms.For(TraverseMode.PassAllDestroyableThings);
            bool CanReachTarget(IntVec3 c) => !target.IsValid || !target.InBounds(map) || map.reachability.CanReach(c, target, PathEndMode.Touch, reachParms);

            if (RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile, allowFogged: false, extraValidator: CanReachTarget))
            {
                return true;
            }

            // 기지가 막혀 있어 일반 진입 칸이 없으면 서 있을 수 있는 지붕 없는 외곽 칸 중에서 찾는다.
            return CellFinder.TryFindRandomEdgeCellWith(
                c => c.Standable(map) && !c.Roofed(map) && CanReachTarget(c),
                map,
                CellFinder.EdgeRoadChance_Ignore,
                out cell);
        }

        public static bool CanEnterFromEdge(Map map)
        {
            if (map == null || map.IsPocketMap || map.IsTempIncidentMap || !map.CanEverExit) { return false; }
            if (map.Biome != null && map.Biome.inVacuum) { return false; }
            if (map.Tile.Valid && map.Tile.LayerDef != null && map.Tile.LayerDef.isSpace) { return false; }

            return PawnsArrivalModeDefOf.EdgeWalkIn.Worker.CanUseOnMap(map);
        }

        // 진입 칸에 아포크리파와 호위 거대 말벌 무리를 스폰하고 보스 폰을 반환한다. 실패 시 null.
        public static Pawn SpawnApocrypha(Map map, IntVec3 entryCell, float points)
        {
            if (map == null || !entryCell.IsValid || !entryCell.InBounds(map)) { return null; }

            var faction = ResolveBossFaction();
            var boss = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                ApocryphaPawnKindDefOf.VV_Apocrypha,
                faction,
                PawnGenerationContext.NonPlayer,
                map.Tile,
                forceGenerateNewPawn: true));

            // 첫 렌더 전에 1페이즈 생애 단계(그래픽)로 고정한다.
            boss.ageTracker.LockCurrentLifeStageIndex(0);

            var rotation = Rot4.FromAngleFlat((map.Center - entryCell).AngleFlat);
            GenSpawn.Spawn(boss, entryCell, map, rotation);

            // 적대 세력을 하나도 찾지 못한 경우: 무소속 + 영구 광폭(무소속 동물인 거대 말벌에게는 적대하지 않는다).
            if (boss.Faction == null)
            {
                boss.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent, forced: true, transitionSilently: true);
            }

            var count = Mathf.Clamp(Mathf.RoundToInt(points / VVPawnKindDefOf.VV_TitanicHornet.combatPower), 4, 12);
            SpawnHornetEscorts(boss, count, fromMapEdge: false);

            ApocryphaEffecterDefOf.VV_Effecter_ApocryphaArrive.Spawn(boss.Position, map, 1.5f).Cleanup();
            Find.TickManager.slower.SignalForceNormalSpeedShort();

            return boss;
        }

        // 보스 세력: 곤충 세력(모든 세력과 적대) → 아무 적대 세력 → 없음.
        private static Faction ResolveBossFaction()
        {
            var insects = Find.FactionManager.OfInsects;
            if (insects != null && !insects.defeated)
            {
                return insects;
            }

            return Find.FactionManager.RandomEnemyFaction(allowHidden: true, allowDefeated: false, allowNonHumanlike: true);
        }

        // 보스를 경호하는 거대 말벌을 count마리 증원한다(fromMapEdge=true면 맵 외곽에서, 아니면 보스 근처에서).
        public static List<Pawn> SpawnHornetEscorts(Pawn boss, int count, bool fromMapEdge)
        {
            var result = new List<Pawn>();
            if (boss == null || !boss.Spawned || count <= 0) { return result; }

            var map = boss.Map;
            var center = boss.Position;
            if (fromMapEdge)
            {
                if (!RCellFinder.TryFindClosestEdgeCellTo(boss.Position, map, out center) &&
                    !RCellFinder.TryFindRandomPawnEntryCell(out center, map, CellFinder.EdgeRoadChance_Animal))
                {
                    center = boss.Position;
                }
            }

            var hornets = AggressiveAnimalIncidentUtility.GenerateAnimals(VVPawnKindDefOf.VV_TitanicHornet, map.Tile, 0f, count);
            foreach (var hornet in hornets)
            {
                var cell = CellFinder.RandomClosewalkCellNear(center, map, fromMapEdge ? 6 : 8);
                GenSpawn.Spawn(hornet, cell, map, Rot4.FromAngleFlat((boss.Position - cell).AngleFlat));

                // 상시 스카리아 기버가 곧 광폭화시키지만, 등장 즉시 적대하도록 광폭 상태를 바로 시작한다.
                hornet.mindState.mentalStateHandler.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk, transitionSilently: true);
                result.Add(hornet);
            }

            if (result.Count == 0) { return result; }

            var lord = FindEscortLord(boss);
            if (lord == null)
            {
                var props = boss.TryGetComp<CompApocrypha>()?.Props;
                LordMaker.MakeNewLord(null, new LordJob_ApocryphaEscort(boss, props?.escortGuardRadius ?? 10f, props?.escortRetaliationRadius ?? 25f), map, result);
            }
            else
            {
                lord.AddPawns(result);
            }

            if (fromMapEdge)
            {
                ApocryphaEffecterDefOf.VV_Effecter_ApocryphaArrive.Spawn(center, map).Cleanup();
            }

            return result;
        }

        // 보스전 동안 맵 외곽에서 걸어 들어와 정착지를 습격하는 거대 말벌(호위 로드가 아닌 일반 말벌 습격 로드). 진입 칸이 없으면 빈 목록.
        public static List<Pawn> SpawnEdgeHornets(Map map, int count)
        {
            var result = new List<Pawn>();
            if (map == null || count <= 0) { return result; }

            if (!RCellFinder.TryFindRandomPawnEntryCell(out var entryCell, map, CellFinder.EdgeRoadChance_Animal)) { return result; }

            var parms = new IncidentParms()
            {
                target = map,
                raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
                spawnCenter = entryCell,
                sendLetter = false,
            };

            result.AddRange(AggressiveAnimalIncidentUtility.GenerateAnimals(VVPawnKindDefOf.VV_TitanicHornet, map.Tile, 0f, count));
            if (result.Count == 0) { return result; }

            parms.raidArrivalMode.Worker.Arrive(result, parms);
            foreach (var hornet in result)
            {
                hornet.mindState.mentalStateHandler.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk, transitionSilently: true);
            }

            LordMaker.MakeNewLord(null, new LordJob_HornetAssault(), map, result);
            return result;
        }

        public static Lord FindEscortLord(Pawn boss)
        {
            if (boss == null) { return null; }

            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var lords = maps[i].lordManager.lords;
                for (int j = 0; j < lords.Count; j++)
                {
                    if (lords[j].LordJob is LordJob_ApocryphaEscort escortJob && escortJob.Boss == boss)
                    {
                        return lords[j];
                    }
                }
            }

            return null;
        }

        // 보스를 경호 중인 거대 말벌 목록(복사본).
        public static List<Pawn> GetEscorts(Pawn boss)
        {
            var lord = FindEscortLord(boss);
            return lord == null ? new List<Pawn>() : new List<Pawn>(lord.ownedPawns);
        }

        // 보스가 쓰러지면 호위 로드를 즉시 일반 말벌 습격 토일로 넘긴다(초점이 null인 호위 듀티가 남지 않게).
        public static void ReleaseEscorts(Pawn boss)
        {
            var lord = FindEscortLord(boss);
            if (lord?.Graph == null) { return; }

            foreach (var toil in lord.Graph.lordToils)
            {
                if (toil is LordToil_HornetAssault && lord.CurLordToil != toil)
                {
                    lord.GotoToil(toil);
                    break;
                }
            }
        }

        // [C 참고] 보스가 처치되어 소멸 연출 중인지(보스 폰은 이미 파괴됨). 이 동안에는 "보스 증발" 감지로 엔딩을 끝내지 말 것.
        public static bool IsDefeatInProgress(Map map)
        {
            return map != null && map.listerThings.ThingsOfDef(ApocryphaThingDefOf.VV_DyingApocrypha).Count > 0;
        }

        // 지금 보고 있는 맵에서 일어난 일일 때만 화면을 흔든다.
        public static void ShakeCamera(Map map, float magnitude)
        {
            if (map != null && map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(magnitude);
            }
        }
    }
}
