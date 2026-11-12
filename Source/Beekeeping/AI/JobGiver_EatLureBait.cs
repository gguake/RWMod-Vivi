using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class JobGiver_EatLureBait : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var lure = (pawn.GetLord()?.LordJob as LordJob_ViviBaitThief)?.Lure;
            if (lure == null || !lure.Spawned || lure.Map != pawn.Map) { return null; }
            if (!WantsBait(pawn) || !TryFindBait(pawn, lure, checkReservation: true, out var bait)) { return null; }

            var nutrition = FoodUtility.GetNutrition(pawn, bait, bait.def);
            var job = JobMaker.MakeJob(JobDefOf.Ingest, bait);
            job.count = FoodUtility.WillIngestStackCountOf(pawn, bait.def, nutrition);
            return job;
        }

        public static bool WantsBait(Pawn pawn) => pawn.needs?.food != null && pawn.needs.food.CurCategory != HungerCategory.Fed;

        public static bool TryFindBait(Pawn pawn, Building_ViviLure lure, bool checkReservation, out Thing bait)
        {
            foreach (var thing in lure.BaitThings)
            {
                if (!IsEdibleBait(pawn, thing)) { continue; }
                if (checkReservation && !pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Deadly)) { continue; }

                bait = thing;
                return true;
            }

            bait = null;
            return false;
        }

        private static bool IsEdibleBait(Pawn pawn, Thing thing)
        {
            return thing.IngestibleNow
                && thing.def.IsNutritionGivingIngestible
                && !thing.def.IsDrug
                && thing.def.ingestible.preferability > FoodPreferability.NeverForNutrition
                && pawn.WillEat(thing);
        }
    }
}
