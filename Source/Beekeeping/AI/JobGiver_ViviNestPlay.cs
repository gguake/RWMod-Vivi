using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 낮에 쉬는 둥지 비비가 둥지 편자 막대에서 바닐라 편자 던지기를 한다.
    // NPC는 즐거움 욕구가 없을 수 있어 바닐라 모임처럼 즐거움이 차도 끝나지 않게 하고, 놀이 시간이 다하면 끝난다.
    public class JobGiver_ViviNestPlay : ThinkNode_JobGiver
    {
        private const float PlayChance = 0.35f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }
            if (!Rand.Chance(PlayChance)) { return null; }

            var jobDef = VVJobDefOf.Play_Horseshoes;
            foreach (var building in pawn.Map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.def != VVThingDefOf.HorseshoesPin || building.Faction != nest.Faction) { continue; }
                if (building.IsForbidden(pawn) || !pawn.CanReserve(building, jobDef.joyMaxParticipants)) { continue; }
                if (!WatchBuildingUtility.TryFindBestWatchCell(building, pawn, desireSit: false, out var cell, out var chair)) { continue; }

                var job = JobMaker.MakeJob(jobDef, building, cell, chair);
                job.doUntilGatheringEnded = true;
                return job;
            }

            return null;
        }
    }
}
