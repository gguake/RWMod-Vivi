using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestFeedPatient : ThinkNode_JobGiver
    {
        private static WorkGiver_FeedPatient Worker => VVWorkGiverDefOf.DoctorFeedHumanlikes?.Worker as WorkGiver_FeedPatient;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null || Worker == null) { return null; }

            var members = nest.lord.ownedPawns;
            for (int i = 0; i < members.Count; ++i)
            {
                var patient = members[i];
                if (patient == pawn || !patient.Spawned || !BeekeepingUtility.NeedsNestFeeding(patient) || !FeedPatientUtility.IsHungry(patient)) { continue; }
                if (!pawn.CanReserveAndReach(patient, PathEndMode.ClosestTouch, Danger.Some)) { continue; }

                var job = Worker.JobOnThing(pawn, patient);
                if (job != null) { return job; }
            }

            return null;
        }
    }
}
