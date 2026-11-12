using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestTend : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) { return null; }

            var members = nest.lord.ownedPawns;
            for (int i = 0; i < members.Count; ++i)
            {
                var patient = members[i];
                if (patient == pawn || !patient.Spawned || !patient.health.HasHediffsNeedingTend()) { continue; }

                if (!patient.Downed && patient.GetPosture() == PawnPosture.Standing) { continue; }
                if (!pawn.CanReserveAndReach(patient, PathEndMode.ClosestTouch, Danger.Some)) { continue; }

                return JobMaker.MakeJob(JobDefOf.TendPatient, patient, FindMedicine(pawn, nest));
            }

            return null;
        }

        private static Thing FindMedicine(Pawn pawn, LordJob_ViviNest nest)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Medicine),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn, Danger.Some),
                validator: t => nest.Stock.IsNestOwned(t) && pawn.CanReserve(t));
        }
    }
}
