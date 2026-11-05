using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 채집 장소의 채집 지시서를 기존 채집 워크기버로 그대로 처리한다. 밤과 바깥이 위험할 때는 쉰다.
    public class JobGiver_ViviNestGather : ThinkNode_JobGiver
    {
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

            return (VVWorkGiverDefOf.VV_DoBillsGathering.Worker as WorkGiver_Scanner)?.JobOnThing(pawn, spot);
        }
    }
}
