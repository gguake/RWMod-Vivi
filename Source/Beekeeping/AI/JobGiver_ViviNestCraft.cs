using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestCraft : ThinkNode_JobGiver
    {
        private static List<WorkGiver_DoBill> _workers;
        private static readonly List<Building_WorkTable> _tmpSpots = new List<Building_WorkTable>();

        private static List<WorkGiver_DoBill> Workers
        {
            get
            {
                if (_workers == null)
                {
                    _workers = new List<WorkGiver_DoBill>
                    {
                        MakeWorker(WorkTypeDefOf.Crafting),
                        MakeWorker(VVWorkTypeDefOf.Cooking),
                    };
                }
                return _workers;
            }
        }

        private static WorkGiver_DoBill MakeWorker(WorkTypeDef workType)
        {
            var def = new WorkGiverDef
            {
                defName = "VV_ViviNestDoBills_" + workType.defName,
                giverClass = typeof(WorkGiver_DoBill),
                workType = workType,
                fixedBillGiverDefs = new List<ThingDef> { VVThingDefOf.VV_ViviNestWorkSpot },
            };
            return (WorkGiver_DoBill)def.Worker;
        }

        public override float GetPriority(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return 0f; }

            return nest.Production.CraftPriority(pawn);
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }

            var production = nest.Production;
            _tmpSpots.Clear();
            foreach (var spot in production.WorkSpots)
            {
                if (spot.Spawned) { _tmpSpots.Add(spot); }
            }
            var position = pawn.Position;
            _tmpSpots.SortBy(s => s.Position.DistanceToSquared(position));

            Job best = null;
            var bestPriority = -1f;
            foreach (var spot in _tmpSpots)
            {
                foreach (var worker in Workers)
                {
                    var job = worker.JobOnThing(pawn, spot);
                    if (job == null || !production.CanStartBill(pawn, job.bill)) { continue; }

                    var priority = production.CraftJobPriority(pawn, job);
                    if (priority > bestPriority)
                    {
                        best = job;
                        bestPriority = priority;
                    }
                }
            }

            return best;
        }
    }
}
