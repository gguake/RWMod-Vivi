using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 채집 자리의 채집 지시서를 기존 채집 워크기버로 그대로 처리한다. 밤과 바깥이 위험할 때는 쉰다.
    // 기존 채집 워크기버 정의는 플레이어 채집 장소·작업대만 받으므로(fixedBillGiverDefs), 둥지 채집 자리만 받는 워크기버를 따로 둔다
    // (플레이어 작업 탭에 나오지 않게 정의 목록에는 넣지 않는다, 제작과 같다).
    public class JobGiver_ViviNestGather : ThinkNode_JobGiver
    {
        private static WorkGiver_GatheringBill _worker;

        private static WorkGiver_GatheringBill Worker
        {
            get
            {
                if (_worker == null)
                {
                    var def = new WorkGiverDef
                    {
                        defName = "VV_ViviNestDoBillsGathering",
                        giverClass = typeof(WorkGiver_GatheringBill),
                        workType = VVWorkTypeDefOf.VV_Gathering,
                        fixedBillGiverDefs = new List<ThingDef> { VVThingDefOf.VV_ViviNestGatherSpot },
                    };
                    _worker = (WorkGiver_GatheringBill)def.Worker;
                }
                return _worker;
            }
        }

        // 채집 지시서 중 가장 모자란 자원의 모자란 정도
        public override float GetPriority(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return 0f; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return 0f; }

            return nest.Production.GatherPriority();
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }

            var spot = nest.Production.GatherSpot;
            if (spot == null) { return null; }

            return Worker.JobOnThing(pawn, spot);
        }
    }
}
