using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 미끼 도둑(곰)은 유인통 위 미끼를 바닐라 섭취로 먹는다. 굶주린 채로 오므로 배가 찰 때까지(또는 먹을 미끼가 없을 때까지) 먹는다.
    public class JobGiver_EatLureBait : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var lure = (pawn.GetLord()?.LordJob as LordJob_ViviBaitThief)?.Lure;
            if (lure == null || !lure.Spawned || lure.Map != pawn.Map) { return null; }
            if (!WantsBait(pawn) || !TryFindBait(pawn, lure, checkReservation: true, out var bait)) { return null; }

            // 바닐라 JobGiver_GetFood와 같은 방식으로 먹을 개수를 정한다.
            var nutrition = FoodUtility.GetNutrition(pawn, bait, bait.def);
            var job = JobMaker.MakeJob(JobDefOf.Ingest, bait);
            job.count = FoodUtility.WillIngestStackCountOf(pawn, bait.def, nutrition);
            return job;
        }

        // 아직 배가 고프면(배부름 단계가 아니면) 미끼를 더 먹는다.
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

        // 바닐라 먹이 찾기처럼 영양을 주는 음식만(약·영양 없음 제외), 식성·음식 제한은 WillEat로 본다.
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
