using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestRescue : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }

            var members = nest.lord.ownedPawns;
            for (int i = 0; i < members.Count; ++i)
            {
                var patient = members[i];
                if (patient == pawn || !patient.Spawned || patient.DevelopmentalStage.Baby() || !patient.Downed) { continue; }

                var rooms = nest.ComfortableRooms(patient);
                var room = patient.GetRoom();
                if (room != null && rooms.Contains(room)) { continue; }
                if (!HealthAIUtility.CanRescueNow(pawn, patient) || !nest.TryFindRoomCell(pawn, rooms, out var cell)) { continue; }

                var job = JobMaker.MakeJob(JobDefOf.HaulToCell, patient, cell);
                job.count = 1;
                job.haulMode = HaulMode.ToCellNonStorage;
                return job;
            }

            return null;
        }
    }
}
