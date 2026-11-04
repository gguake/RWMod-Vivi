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

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }

            var spot = nest.Production.WorkSpot;
            if (spot == null) { return null; }

            foreach (var worker in Workers)
            {
                var job = worker.JobOnThing(pawn, spot);
                if (job != null) { return job; }
            }

            return null;
        }
    }
}
