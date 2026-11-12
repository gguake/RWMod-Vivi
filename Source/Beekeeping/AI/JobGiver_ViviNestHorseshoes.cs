using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
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
                return pin != null && pin.Spawned && ThinkNode_ViviNestPlay.Elapsed(current) < PlayTicks && ThinkNode_ViviNestPlay.IsOutdoorPlaySafe(pawn, current.targetB.Cell) ? current : null;
            }

            if (!ThinkNode_ViviNestPlay.IsOutdoorPlayWeatherSafe(pawn, out var comfortable)) { return null; }

            var map = pawn.Map;
            var jobDef = VVJobDefOf.Play_Horseshoes;
            foreach (var building in map.listerBuildings.allBuildingsNonColonist)
            {
                if (building.def != VVThingDefOf.HorseshoesPin || building.Faction != nest.Faction) { continue; }
                if (building.IsForbidden(pawn) || !pawn.CanReserve(building, jobDef.joyMaxParticipants)) { continue; }
                if (!WatchBuildingUtility.TryFindBestWatchCell(building, pawn, desireSit: false, out var cell, out var chair)) { continue; }
                if (!comfortable.Includes(GenTemperature.GetTemperatureForCell(cell, map))) { continue; }

                var job = JobMaker.MakeJob(jobDef, building, cell, chair);
                job.doUntilGatheringEnded = true;
                ThinkNode_ViviNestPlay.SetRethink(job);
                return job;
            }

            return null;
        }
    }
}
