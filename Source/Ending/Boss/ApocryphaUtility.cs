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
            return pawn != null && pawn.def == VVThingDefOf.VV_Apocrypha;
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

        public static Pawn GenerateApocrypha(Map map)
        {
            var boss = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                VVPawnKindDefOf.VV_Apocrypha,
                ResolveBossFaction(),
                PawnGenerationContext.NonPlayer,
                map.Tile,
                forceGenerateNewPawn: true));

            boss.ageTracker.LockCurrentLifeStageIndex(0);
            return boss;
        }

        public static void SpawnApocrypha(Pawn boss, Map map, IntVec3 entryCell)
        {
            GenSpawn.Spawn(boss, entryCell, map, EntryRotation(map, entryCell));

            if (boss.Faction == null)
            {
                boss.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent, forced: true, transitionSilently: true);
            }
        }

        // 보스 스폰과 분리해 호위·연출이 실패해도 이미 스폰된 보스로 전투를 시작할 수 있게 한다.
        // 파장에 밀려 외곽에 내려앉은 보스 둘레에서 호위 말벌이 검은 기운으로 빚어진다.
        public static void SpawnArrival(Pawn boss)
        {
            var count = boss.TryGetComp<CompApocrypha>()?.ArrivalHornetCount ?? 0;
            HornetManifestation.SummonAround(boss, count, HornetSummonRole.Escort);

            VVEffecterDefOf.VV_Effecter_ApocryphaArrive.Spawn(boss.Position, boss.Map, 1.5f).Cleanup();
            Find.TickManager.slower.SignalForceNormalSpeedShort();
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

        // 보스전 말벌은 모두 오염된 영원꽃 동화를 지닌 채 스폰 즉시 적대하도록 광폭 상태로 시작한다.
        public static Pawn SpawnBossFightHornet(Map map, IntVec3 cell, Rot4 rotation)
        {
            var hornet = PawnGenerator.GeneratePawn(new PawnGenerationRequest(VVPawnKindDefOf.VV_TitanicHornet, null, PawnGenerationContext.NonPlayer, map.Tile));
            hornet.health.AddHediff(VVHediffDefOf.VV_CorruptedEverflowerAssimilation);
            GenSpawn.Spawn(hornet, cell, map, rotation);

            hornet.mindState.mentalStateHandler.TryStartMentalState(VVMentalStateDefOf.VV_HornetBerserk, transitionSilently: true);
            return hornet;
        }

        public static void JoinEscort(Pawn boss, Pawn hornet)
        {
            var lord = FindEscortLord(boss);
            if (lord == null)
            {
                var props = boss.TryGetComp<CompApocrypha>()?.Props;
                LordMaker.MakeNewLord(null, new LordJob_ApocryphaEscort(boss, props?.escortGuardRadius ?? 10f, props?.escortRetaliationRadius ?? 25f), hornet.Map, new List<Pawn> { hornet });
                return;
            }

            lord.AddPawn(hornet);
        }

        public static void JoinFlowerGuard(Pawn boss, Thing flower, Pawn hornet)
        {
            var lord = FindFlowerGuardLord(boss);
            if (lord == null)
            {
                var props = boss.TryGetComp<CompApocrypha>()?.Props;
                var guardJob = new LordJob_ApocryphaFlowerGuard(boss, props?.flowerGuardRadius ?? 6f, props?.flowerGuardRetaliationRadius ?? 20f);
                guardJob.AssignFlower(hornet, flower);
                LordMaker.MakeNewLord(null, guardJob, hornet.Map, new List<Pawn> { hornet });
                return;
            }

            ((LordJob_ApocryphaFlowerGuard)lord.LordJob).AssignFlower(hornet, flower);
            lord.AddPawn(hornet);
        }

        // 정착지 습격은 일반 거대 말벌 강습과 같은 로드를 쓰므로 맵에 있는 강습 로드에 함께 넣는다.
        public static void JoinHornetAssault(Map map, List<Pawn> hornets)
        {
            if (map == null || hornets.NullOrEmpty()) { return; }

            var joining = new List<Pawn>();
            foreach (var hornet in hornets)
            {
                var previous = hornet.GetLord();
                previous?.Notify_PawnLost(hornet, PawnLostCondition.ForcedToJoinOtherLord);
                if (hornet.GetLord() == null)
                {
                    joining.Add(hornet);
                }
            }

            if (joining.Count == 0) { return; }

            var lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                if (lords[i].LordJob is LordJob_HornetAssault)
                {
                    lords[i].AddPawns(joining);
                    return;
                }
            }

            LordMaker.MakeNewLord(null, new LordJob_HornetAssault(), map, joining);
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
            return map != null && map.listerThings.ThingsOfDef(VVThingDefOf.VV_DyingApocrypha).Count > 0;
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
