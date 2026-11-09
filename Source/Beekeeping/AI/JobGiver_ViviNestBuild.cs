using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 청사진·골조는 바닐라 건설 워크기버로 짓고, 손상된 둥지 건물은 바닐라 수리 워크기버로 고치며, 빈 지붕은 둥지 지붕 잡으로 얹는다.
    // 포장 식물의 설치 청사진도 같은 청사진 워크기버가 설치한다(포장을 가져가 세우는 운반 잡). 맵 전체 청사진을 훑는 JobGiver_Work 대신 둥지 것만 넘긴다.
    public class JobGiver_ViviNestBuild : ThinkNode_JobGiver
    {
        private static readonly List<Thing> _candidates = new List<Thing>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) { return null; }
            // 아기로 무리에 든 구성원은 그때 작업 설정을 켜지 않았으므로, 아이로 자란 뒤 바닐라 건설 판정 전에 켠다.
            if (pawn.workSettings != null && !pawn.workSettings.EverWork) { EnsureBuilderWorkSettings(pawn); }

            return ConstructionJob(pawn, nest) ?? RepairJob(pawn, nest) ?? RoofJob(pawn, nest);
        }

        private static Job ConstructionJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var finish = VVWorkGiverDefOf.ConstructFinishFrames.Worker as WorkGiver_Scanner;
            var deliverFrames = VVWorkGiverDefOf.ConstructDeliverResourcesToFrames.Worker as WorkGiver_Scanner;
            var deliverBlueprints = VVWorkGiverDefOf.ConstructDeliverResourcesToBlueprints.Worker as WorkGiver_Scanner;

            // 가까운 것부터 본다. 후보 목록을 재사용해 생각할 때마다 할당하지 않는다.
            var position = pawn.Position;
            _candidates.Clear();
            foreach (var thing in nest.Planner.CachedNestConstructions())
            {
                if (thing.Spawned) { _candidates.Add(thing); }
            }
            _candidates.SortBy(t => t.Position.DistanceToSquared(position));

            var stock = nest.Stock;
            foreach (var thing in _candidates)
            {
                Job job;
                if (thing is Frame)
                {
                    job = finish.JobOnThing(pawn, thing);
                    if (job == null && !LacksMaterials(thing, stock, pawn)) { job = deliverFrames.JobOnThing(pawn, thing); }
                }
                else
                {
                    job = LacksMaterials(thing, stock, pawn) ? null : deliverBlueprints.JobOnThing(pawn, thing);
                }

                if (job != null) { return job; }
            }
            return null;
        }

        // 바닐라 배달 판정이 잡을 줄 수 없는 청사진·골조: 아직 들어가야 할 재료가 있는데 그중 어느 것도 둥지에 없고(둥지 비비는 둥지 몫만 쓴다,
        // 장부는 둥지 칸·둥지 몫·구성원이 지닌 것을 센다), 재료와 상관없이 주는 잡(자리를 막는 것 치우기, 바닥 걷어내기, 재료 없는 골조 놓기, 설치)도 없는 것.
        // 바닐라 배달 판정은 청사진 하나에 130 µs라(실측, 크림이 없는 크림 벽 청사진 20개) 재료를 기다리는 동안 사고 1회가 최대 4.8 ms였다.
        private static bool LacksMaterials(Thing thing, ViviNestStock stock, Pawn pawn)
        {
            if (thing is Blueprint_Install || !(thing is IConstructible constructible)) { return false; }
            // 바닐라 바닥 걷어내기(WorkGiver_ConstructDeliverResources.ShouldRemoveExistingFloorFirst)
            if (thing is Blueprint && thing.def.entityDefToBuild is TerrainDef && thing.Map.terrainGrid.CanRemoveTopLayerAt(thing.Position)) { return false; }

            var needed = false;
            var costs = constructible.TotalMaterialCost();
            for (int i = 0; i < costs.Count; ++i)
            {
                var def = costs[i].thingDef;
                if (constructible.ThingCountNeeded(def) <= 0) { continue; }
                if (stock.CountOf(def) > 0) { return false; }
                needed = true;
            }

            return needed && GenConstruct.FirstBlockingThing(thing, pawn) == null;
        }

        private static Job RepairJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var repair = VVWorkGiverDefOf.Repair.Worker as WorkGiver_Scanner;
            var repairable = pawn.Map.listerBuildingsRepairable.RepairableBuildings(nest.Faction);
            for (int i = 0; i < repairable.Count; ++i)
            {
                // 수리 워크기버의 JobOnThing은 조건을 보지 않으므로 HasJobOnThing부터 확인한다.
                // HasJobOnThing은 도달을 보지 않는다(바닐라는 JobGiver_Work 스캐너가 워크기버의 경로 끝·위험도로 따로 본다). 같은 판정을 하지 않으면
                // 플레이어 벽에 갇힌 둥지 건물을 골라 경로 실패 → 대기만 되풀이하며 아래 일이 멈춘다.
                var thing = repairable[i];
                if (repair.HasJobOnThing(pawn, thing) && pawn.CanReach(thing, repair.PathEndMode, repair.MaxPathDanger(pawn)))
                {
                    return repair.JobOnThing(pawn, thing);
                }
            }
            return null;
        }

        // 바닐라 지붕 짓기 워크기버와 같은 조건(지붕 받침·막는 것·예약)으로 고른다.
        private static Job RoofJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var map = pawn.Map;
            foreach (var cell in nest.Planner.CellsNeedingRoof())
            {
                if (!ViviNestPlanner.CanRoofNow(cell, map) || !RoofCollapseUtility.ConnectedToRoofHolder(cell, map, assumeRoofAtRoot: true)) { continue; }
                if (!pawn.CanReserve(cell, 1, -1, ReservationLayerDefOf.Ceiling) || !pawn.CanReach(cell, PathEndMode.Touch, Danger.Some)) { continue; }

                return JobMaker.MakeJob(VVJobDefOf.VV_ViviNestBuildRoof, cell, cell);
            }
            return null;
        }

        // 바닐라 건설 판정은 작업 설정의 건설이 켜져 있어야 하므로 공성 건설자처럼 작업 설정을 켠다(둥지 비비는 JobGiver_Work를 쓰지 않는다).
        public static void EnsureBuilderWorkSettings(Pawn pawn)
        {
            if (pawn.workSettings == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) { return; }

            if (!pawn.workSettings.EverWork)
            {
                pawn.workSettings.EnableAndInitialize();
            }
            pawn.workSettings.SetPriority(WorkTypeDefOf.Construction, 1);
        }
    }
}
