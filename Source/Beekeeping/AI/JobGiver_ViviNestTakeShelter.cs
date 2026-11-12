using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestTakeShelter : ThinkNode_JobGiver
    {
        private const float DangerRadius = 15f;
        private const int FleeDistance = 12;
        private const int WaitTicks = 180;
        private const int ShelterSleepTicks = 2000;
        private const float TiredRestLevel = 0.3f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.Downed || pawn.DevelopmentalStage.Baby()) { return null; }
            if (!GenAI.EnemyIsNear(pawn, DangerRadius, out var threat)) { return null; }

            var territory = nest.Territory;
            var threatInRoom = territory.IsRoomCell(threat.Position);
            if (!threatInRoom)
            {
                if (IsShelterMeal(pawn)) { return null; }

                if (territory.IsRoomCell(pawn.Position))
                {
                    var need = TryGiveNeedJobInRoom(pawn);
                    if (need != null) { return need; }

                    var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat, WaitTicks);
                    wait.checkOverrideOnExpire = true;
                    return wait;
                }

                if (nest.TryGetRandomRoomCell(out var cell) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                    job.locomotionUrgency = LocomotionUrgency.Sprint;
                    return job;
                }
            }

            return FleeUtility.FleeJob(pawn, threat, FleeDistance);
        }

        private static Job TryGiveNeedJobInRoom(Pawn pawn)
        {
            var food = pawn.needs?.food;
            if (food != null && food.CurLevelPercentage < pawn.RaceProps.FoodLevelPercentageWantEat)
            {
                var meal = FindFoodInRoom(pawn, food.CurCategory == HungerCategory.Starving);
                if (meal != null)
                {
                    var job = JobMaker.MakeJob(JobDefOf.Ingest, meal);
                    job.count = FoodUtility.WillIngestStackCountOf(pawn, meal.def, FoodUtility.GetNutrition(pawn, meal, meal.def));
                    return job;
                }
            }

            if (WantsToSleep(pawn))
            {
                var sleep = JobMaker.MakeJob(JobDefOf.Wait, ShelterSleepTicks);
                sleep.forceSleep = true;
                sleep.reportStringOverride = JobDefOf.Wait_Asleep.reportString;
                return sleep;
            }

            return null;
        }

        private static Thing FindFoodInRoom(Pawn pawn, bool desperate)
        {
            var map = pawn.Map;
            var room = pawn.GetRoom();
            if (room == null) { return null; }

            Thing best = null;
            var bestScore = float.MinValue;
            foreach (var cell in room.Cells)
            {
                var things = map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; ++i)
                {
                    var thing = things[i];
                    if (thing.def.category != ThingCategory.Item || thing is Corpse || !thing.def.IsNutritionGivingIngestible || thing.def.IsDrug) { continue; }
                    if (!thing.IngestibleNow || thing.IsDessicated() || (!desperate && thing.IsNotFresh())) { continue; }
                    if (!pawn.WillEat(thing) || thing.IsForbidden(pawn) || thing.IsBurning()) { continue; }

                    var count = FoodUtility.WillIngestStackCountOf(pawn, thing.def, FoodUtility.GetNutrition(pawn, thing, thing.def));
                    if (!pawn.CanReserve(thing, 10, FoodUtility.GetMaxAmountToPickup(thing, pawn, count)) || !pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.Some)) { continue; }

                    var score = FoodUtility.FoodOptimality(pawn, thing, thing.def, (pawn.Position - thing.Position).LengthHorizontal);
                    if (score > bestScore)
                    {
                        best = thing;
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        private static bool WantsToSleep(Pawn pawn)
        {
            var rest = pawn.needs?.rest;
            if (rest == null || Find.TickManager.TicksGame < pawn.mindState.canSleepTick) { return false; }
            if (!RestUtility.CanFallAsleep(pawn) || RestUtility.DisturbancePreventsLyingDown(pawn)) { return false; }

            return JobGiver_ViviNestRest.IsNight(pawn) || rest.CurLevel < TiredRestLevel;
        }

        private static bool IsShelterMeal(Pawn pawn)
        {
            var job = pawn.CurJob;
            return job != null && job.def == JobDefOf.Ingest && job.jobGiver is JobGiver_ViviNestTakeShelter;
        }

        public static bool IsOutsideShelterMealRoom(IntVec3 cell, Pawn pawn)
        {
            if (!IsShelterMeal(pawn) || !pawn.Spawned) { return false; }

            var room = pawn.GetRoom();
            return room != null && cell.GetRoom(pawn.Map) != room;
        }
    }
}
