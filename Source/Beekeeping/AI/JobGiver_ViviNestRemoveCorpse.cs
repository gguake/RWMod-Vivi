using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestRemoveCorpse : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }
            if (BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }

            var corpse = FindNestRoomCorpse(pawn, nest.Territory);
            if (corpse == null || !nest.Planner.TryFindCorpseDumpCell(pawn, out var cell)) { return null; }

            var job = JobMaker.MakeJob(JobDefOf.HaulToCell, corpse, cell);
            job.count = 1;
            job.haulMode = HaulMode.ToCellNonStorage;
            job.ignoreForbidden = true;
            return job;
        }

        private static Corpse FindNestRoomCorpse(Pawn pawn, ViviNestTerritory territory)
        {
            var map = pawn.Map;
            var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            if (corpses.Count == 0) { return null; }

            var roomCells = territory.OwnedRoomCells;
            if (corpses.Count <= roomCells.Count)
            {
                for (int i = 0; i < corpses.Count; ++i)
                {
                    if (corpses[i] is Corpse corpse && territory.IsNestRoomCell(corpse.Position) && CanRemove(pawn, corpse)) { return corpse; }
                }
                return null;
            }

            for (int i = 0; i < roomCells.Count; ++i)
            {
                var things = map.thingGrid.ThingsListAtFast(roomCells[i]);
                for (int j = 0; j < things.Count; ++j)
                {
                    if (things[j] is Corpse corpse && CanRemove(pawn, corpse)) { return corpse; }
                }
            }
            return null;
        }

        private static bool CanRemove(Pawn pawn, Corpse corpse)
        {
            return !corpse.IsBurning() && pawn.CanReserveAndReach(corpse, PathEndMode.ClosestTouch, Danger.Some);
        }
    }
}
