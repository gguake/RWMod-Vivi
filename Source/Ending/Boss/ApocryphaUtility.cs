using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public static class ApocryphaUtility
    {
        public static bool IsApocrypha(Pawn pawn)
        {
            return pawn != null && pawn.def == ApocryphaThingDefOf.VV_Apocrypha;
        }

        public static bool TryFindEntryCell(Map map, IntVec3 target, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (!CanEnterFromEdge(map)) { return false; }

            // 구조물을 부수며 진입할 수 있으므로 파괴 가능한 장애물은 통과 가능한 것으로 보고 경로를 검사한다.
            var reachParms = TraverseParms.For(TraverseMode.PassAllDestroyableThings);
            bool CanReachTarget(IntVec3 c) => !target.IsValid || !target.InBounds(map) || map.reachability.CanReach(c, target, PathEndMode.Touch, reachParms);

            if (RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile, allowFogged: false, extraValidator: CanReachTarget))
            {
                return true;
            }

            // 미탐사 바위 골짜기에 갇혀 등장하지 않도록 안개 칸을 제외한다. 유효한 칸이 없으면 TryFindBreachEntryCell로 넘어간다.
            return CellFinder.TryFindRandomEdgeCellWith(
                c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && CanReachTarget(c),
                map,
                CellFinder.EdgeRoadChance_Ignore,
                out cell);
        }

        public static bool TryFindBreachEntryCell(Map map, Thing target, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (target == null || !CanEnterFromEdge(map)) { return false; }

            var targetRect = TargetRect(map, target);
            var targetCell = targetRect.CenterCell;
            var bestTier = int.MaxValue;
            var bestDistance = int.MaxValue;
            foreach (var c in CellRect.WholeMap(map).EdgeCells)
            {
                if (!c.Standable(map)) { continue; }

                var tier = (c.Fogged(map) ? 1 : 0) + (IsBreachableLine(map, c, targetRect) ? 0 : 2);
                if (tier > bestTier) { continue; }

                var distance = c.DistanceToSquared(targetCell);
                if (tier < bestTier || distance < bestDistance)
                {
                    cell = c;
                    bestTier = tier;
                    bestDistance = distance;
                }
            }

            return cell.IsValid;
        }

        public static CellRect TargetRect(Map map, Thing target)
        {
            return target.Spawned && target.Map == map ? target.OccupiedRect() : CellRect.SingleCell(target.PositionHeld);
        }

        public static bool IsBreachableLine(Map map, IntVec3 from, CellRect targetRect)
        {
            foreach (var c in GenSight.PointsOnLineOfSight(from, targetRect.CenterCell))
            {
                if (targetRect.Contains(c)) { return true; }
                if (!IsBreachableCell(map, c)) { return false; }
            }

            return true;
        }

        public static bool IsBreachableCell(Map map, IntVec3 cell)
        {
            if (!cell.InBounds(map) || cell.GetTerrain(map).passability == Traversability.Impassable) { return false; }

            var edifice = cell.GetEdifice(map);
            return edifice == null || !BlocksPath(edifice) || JobGiver_ApocryphaMain.CanSmash(edifice, null);
        }

        public static bool BlocksPath(Thing thing)
        {
            return thing is Building && (thing.def.passability == Traversability.Impassable || thing is Building_Door);
        }

        public static bool CanEnterFromEdge(Map map)
        {
            if (map == null || map.IsPocketMap || map.IsTempIncidentMap || !map.CanEverExit) { return false; }
            if (map.Biome != null && map.Biome.inVacuum) { return false; }
            if (map.Tile.Valid && map.Tile.LayerDef != null && map.Tile.LayerDef.isSpace) { return false; }

            return PawnsArrivalModeDefOf.EdgeWalkIn.Worker.CanUseOnMap(map);
        }

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

            boss.ageTracker.LockCurrentLifeStageIndex(0);

            GenSpawn.Spawn(boss, entryCell, map, EntryRotation(map, entryCell));

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

        public static Rot4 EntryRotation(Map map, IntVec3 entryCell)
        {
            return Rot4.FromAngleFlat((map.Center - entryCell).AngleFlat);
        }

        private static Faction ResolveBossFaction()
        {
            var insects = Find.FactionManager.OfInsects;
            if (insects != null && !insects.defeated)
            {
                return insects;
            }

            return Find.FactionManager.RandomEnemyFaction(allowHidden: true, allowDefeated: false, allowNonHumanlike: true);
        }

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

                // 상시 스카리아 기버의 다음 처리를 기다리지 않고 스폰 즉시 적대하도록 광폭 상태를 시작한다.
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

        public static List<Pawn> SpawnFlowerGuardHornets(Pawn boss, int count)
        {
            if (boss == null || !boss.Spawned) { return new List<Pawn>(); }

            var map = boss.Map;
            var result = WalkInHornets(map, count);
            if (result.Count == 0) { return result; }

            var lord = FindFlowerGuardLord(boss);
            if (lord == null)
            {
                var props = boss.TryGetComp<CompApocrypha>()?.Props;
                LordMaker.MakeNewLord(null, new LordJob_ApocryphaFlowerGuard(
                    boss,
                    props?.flowerGuardRadius ?? 6f,
                    props?.flowerGuardRetaliationRadius ?? 20f,
                    props?.escortGuardRadius ?? 10f), map, result);
            }
            else
            {
                lord.AddPawns(result);
            }

            return result;
        }

        private static List<Pawn> WalkInHornets(Map map, int count)
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

            return result;
        }

        public static Lord FindEscortLord(Pawn boss)
        {
            return FindBossLord(boss, lordJob => lordJob is LordJob_ApocryphaEscort escortJob && escortJob.Boss == boss);
        }

        public static Lord FindFlowerGuardLord(Pawn boss)
        {
            return FindBossLord(boss, lordJob => lordJob is LordJob_ApocryphaFlowerGuard guardJob && guardJob.Boss == boss);
        }

        private static Lord FindBossLord(Pawn boss, System.Predicate<LordJob> match)
        {
            if (boss == null) { return null; }

            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var lords = maps[i].lordManager.lords;
                for (int j = 0; j < lords.Count; j++)
                {
                    if (match(lords[j].LordJob))
                    {
                        return lords[j];
                    }
                }
            }

            return null;
        }

        public static void ClearEscortFocus(Pawn boss)
        {
            (FindEscortLord(boss)?.LordJob as LordJob_ApocryphaEscort)?.ClearFocus();
        }

        public static List<Pawn> GetEscorts(Pawn boss)
        {
            var lord = FindEscortLord(boss);
            return lord == null ? new List<Pawn>() : new List<Pawn>(lord.ownedPawns);
        }

        // 대상이 null인 호위 듀티가 남지 않도록 보스 토벌 시 호위·꽃 경비 로드를 일반 습격 토일로 전환한다.
        public static void ReleaseEscorts(Pawn boss)
        {
            GotoAssault(FindEscortLord(boss));
            GotoAssault(FindFlowerGuardLord(boss));
        }

        private static void GotoAssault(Lord lord)
        {
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

        // 소멸 연출 중에는 보스 폰이 이미 파괴되어 있으므로 보스 소실 감지로 엔딩을 종료하면 안 된다.
        public static bool IsDefeatInProgress(Map map)
        {
            return map != null && map.listerThings.ThingsOfDef(ApocryphaThingDefOf.VV_DyingApocrypha).Count > 0;
        }

        public static void ShakeCamera(Map map, float magnitude)
        {
            if (map != null && map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(magnitude);
            }
        }
    }
}
