using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 놀이 묶음의 편자 막대 놀이: 둥지 편자 막대(바깥)에서 바닐라 편자 던지기를 한다.
    // 바깥이 위험하거나 기온이 그 비비의 편한 범위 밖이거나 저체온·열사병 기미가 있으면 하지 않고(놀이 묶음이 실내 놀이를 고른다), 하던 중에 그렇게 되면 그만둔다.
    // NPC는 즐거움 욕구가 없을 수 있어 바닐라 모임처럼 즐거움이 차도 끝나지 않게 하고, 놀이 시간이 다하면 다른 놀이를 고른다.
    public class JobGiver_ViviNestHorseshoes : ThinkNode_JobGiver
    {
        private const int PlayTicks = 2000;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null) { return null; }

            var current = ThinkNode_ViviNestPlay.CurrentPlay(pawn, this);
            if (current != null)
            {
                var pin = current.targetA.Thing;
                return pin != null && pin.Spawned && ThinkNode_ViviNestPlay.Elapsed(current) < PlayTicks && ThinkNode_ViviNestPlay.OutdoorPlayOk(pawn, current.targetB.Cell) ? current : null;
            }

            var jobDef = VVJobDefOf.Play_Horseshoes;
            foreach (var building in pawn.Map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.def != VVThingDefOf.HorseshoesPin || building.Faction != nest.Faction) { continue; }
                if (building.IsForbidden(pawn) || !pawn.CanReserve(building, jobDef.joyMaxParticipants)) { continue; }
                if (!WatchBuildingUtility.TryFindBestWatchCell(building, pawn, desireSit: false, out var cell, out var chair)) { continue; }
                if (!ThinkNode_ViviNestPlay.OutdoorPlayOk(pawn, cell)) { continue; }

                var job = JobMaker.MakeJob(jobDef, building, cell, chair);
                job.doUntilGatheringEnded = true;
                ThinkNode_ViviNestPlay.SetRethink(job);
                return job;
            }

            return null;
        }
    }
}
