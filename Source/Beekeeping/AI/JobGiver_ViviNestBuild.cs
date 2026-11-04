using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 청사진·골조는 바닐라 건설 워크기버로 짓고, 손상된 둥지 건물은 바닐라 수리 워크기버로 고치며, 빈 지붕은 둥지 지붕 잡으로 얹는다.
    // 맵 전체 청사진을 훑는 JobGiver_Work 대신 둥지 것만 넘긴다.
    public class JobGiver_ViviNestBuild : ThinkNode_JobGiver
    {
        private static WorkGiver_Scanner _finishFrames;
        private static WorkGiver_Scanner _deliverFrames;
        private static WorkGiver_Scanner _deliverBlueprints;
        private static WorkGiver_Scanner _repair;
        private static readonly List<Thing> _candidates = new List<Thing>();

        private static WorkGiver_Scanner Worker(ref WorkGiver_Scanner cache, string defName)
        {
            if (cache == null)
            {
                cache = DefDatabase<WorkGiverDef>.GetNamed(defName).Worker as WorkGiver_Scanner;
            }
            return cache;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) { return null; }

            return ConstructionJob(pawn, nest) ?? RepairJob(pawn, nest) ?? RoofJob(pawn, nest);
        }

        private static Job ConstructionJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var finish = Worker(ref _finishFrames, "ConstructFinishFrames");
            var deliverFrames = Worker(ref _deliverFrames, "ConstructDeliverResourcesToFrames");
            var deliverBlueprints = Worker(ref _deliverBlueprints, "ConstructDeliverResourcesToBlueprints");

            // 가까운 것부터 본다. 후보 목록을 재사용해 생각할 때마다 할당하지 않는다.
            var position = pawn.Position;
            _candidates.Clear();
            foreach (var thing in nest.Planner.CachedNestConstructions())
            {
                if (thing.Spawned) { _candidates.Add(thing); }
            }
            _candidates.SortBy(t => t.Position.DistanceToSquared(position));

            foreach (var thing in _candidates)
            {
                var job = thing is Frame
                    ? finish.JobOnThing(pawn, thing) ?? deliverFrames.JobOnThing(pawn, thing)
                    : deliverBlueprints.JobOnThing(pawn, thing);

                if (job != null) { return job; }
            }
            return null;
        }

        private static Job RepairJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var repair = Worker(ref _repair, "Repair");
            var repairable = pawn.Map.listerBuildingsRepairable.RepairableBuildings(nest.Faction);
            for (int i = 0; i < repairable.Count; ++i)
            {
                // 수리 워크기버의 JobOnThing은 조건을 보지 않으므로 HasJobOnThing부터 확인한다.
                if (repair.HasJobOnThing(pawn, repairable[i]))
                {
                    return repair.JobOnThing(pawn, repairable[i]);
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
