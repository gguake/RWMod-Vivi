using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 바닥 작업 자리의 지시서를 바닐라 작업 지시서 워크기버로 처리한다.
    // 꽃가루 경단처럼 요리로 제한된 레시피가 있어 제작·요리용 워크기버를 따로 둔다(플레이어 작업 탭에 나오지 않게 정의 목록에는 넣지 않는다).
    public class JobGiver_ViviNestCraft : ThinkNode_JobGiver
    {
        private static List<WorkGiver_DoBill> _workers;

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

        // 작업 지시서 중 이 비비가 할 수 있는 가장 모자란 것의 모자란 정도(만들다 만 자기 것은 가장 먼저)
        public override float GetPriority(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return 0f; }

            return nest.Production.CraftPriority(pawn);
        }

        // 워크기버는 자기 작업 종류(제작·요리)의 지시서 중 위의 것을 고르므로, 둘이 고른 것 중 더 모자란 쪽을 한다.
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }

            var spot = nest.Production.WorkSpot;
            if (spot == null) { return null; }

            Job best = null;
            var bestPriority = -1f;
            foreach (var worker in Workers)
            {
                var job = worker.JobOnThing(pawn, spot);
                if (job == null) { continue; }

                var priority = nest.Production.CraftJobPriority(pawn, job);
                if (priority > bestPriority)
                {
                    best = job;
                    bestPriority = priority;
                }
            }

            return best;
        }
    }
}
