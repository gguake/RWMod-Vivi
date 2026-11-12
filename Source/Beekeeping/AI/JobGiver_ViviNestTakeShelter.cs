using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 적·사냥하는 포식자가 가까우면 둥지 방으로 피하고 방 안에 머문다. 위협이 방 안에 있거나 방으로 갈 수 없으면 바닐라 도망으로 멀어진다.
    // 방 안에 머무는 동안에도 배가 고프면 지금 선 방 안의 둥지 음식을 먹고, 졸리면 그 자리에서 잔다(리뷰 D2-3). 대피 대기만 주면 닿을 수 없는 위협
    // (우리에 갇힌 광분 동물 등)이 15칸 안에 오래 머무는 동안 방 안에 꿀이 있어도 굶었다(자동 시험 t10). 상시 노드라 null을 주면 주 트리가 방 밖 음식·일을
    // 골라 나갔다가 다시 끌려 들어오므로, 대피 중 욕구는 이 기버가 지금 선 방 안 대상으로만 직접 준다. 육아처럼 데려다 둘 자리를 잡 실행 중에 정하는 일은 주지 않는다.
    public class JobGiver_ViviNestTakeShelter : ThinkNode_JobGiver
    {
        private const float DangerRadius = 15f;
        private const int FleeDistance = 12;
        private const int WaitTicks = 180;
        // 대피 중 그 자리에서 자는 시간. 끝나면(잡 만료) 다시 판단해 아직 졸리면 또 잔다.
        private const int ShelterSleepTicks = 2000;
        // 바닐라 JobGiver_GetRest가 일과표 없는 폰(Anything)을 재우는 기력
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
                // 대피하며 이 기버가 준 식사는 끝날 때까지 둔다. 이어 주지 않으면 방 안의 마지막 음식을 든 뒤 먹을 것이 더는 안 보여 대피 대기가 식사를 끊고,
                // 떨어뜨린 음식을 다시 집는 일을 되풀이한다. 씹을 자리는 지금 선 방 안으로만 고른다(IsOutsideShelterMealRoom).
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

        // 지금 선 둥지 방 안에서 끝나는 욕구 잡: 배가 고프면(바닐라 식사 잡 기버의 기준) 그 방에 놓인 둥지 음식을 먹고, 졸리면 그 자리에서 잔다.
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
                // 바닐라 기다리기 잡의 강제 수면(눕기 토일, 다른 잡을 찾지 않는다): 바닐라 눕기 잡은 자는 동안 211틱마다 주 트리로 다시 생각해
                // 다른 방 잠자리로 옮겨 가다 대피에 끌려 들어오므로 쓰지 않는다. 만료되면 깨어 이 기버가 다시 판단한다.
                var sleep = JobMaker.MakeJob(JobDefOf.Wait, ShelterSleepTicks);
                sleep.forceSleep = true;
                sleep.reportStringOverride = JobDefOf.Wait_Asleep.reportString;
                return sleep;
            }

            return null;
        }

        // 지금 선 방에 놓인 둥지 음식(둥지 비비에게 금지되지 않은 것) 중 바닐라 식사 판정(FoodOptimality)으로 가장 좋은 것.
        // 바닐라 식사 잡이 실행하자마자 실패하지 않도록 같은 예약 수(JobDriver_Ingest.TryMakePreToilReservations)를 미리 본다.
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

        // 둥지 생활 듀티의 잠(JobGiver_ViviNestRest)과 같은 기준: 잠들 수 있고, 밤이면 기력이 덜 찼을 때, 낮이면 지쳤을 때
        private static bool WantsToSleep(Pawn pawn)
        {
            var rest = pawn.needs?.rest;
            if (rest == null || Find.TickManager.TicksGame < pawn.mindState.canSleepTick) { return false; }
            if (!RestUtility.CanFallAsleep(pawn) || RestUtility.DisturbancePreventsLyingDown(pawn)) { return false; }

            return JobGiver_ViviNestRest.IsNight(pawn) || rest.CurLevel < TiredRestLevel;
        }

        // 지금 잡이 대피 중 이 기버가 준 식사인지
        private static bool IsShelterMeal(Pawn pawn)
        {
            var job = pawn.CurJob;
            return job != null && job.def == JobDefOf.Ingest && job.jobGiver is JobGiver_ViviNestTakeShelter;
        }

        // 대피 중 식사(이 기버가 준 것)를 하는 둥지 비비에게 그 칸이 지금 선 방 밖인지(BeekeepingPatch의 칸 금지 판정이 쓴다).
        // 바닐라 식사 잡은 음식을 든 뒤 실행 중에 씹을 의자·자리를 고르는데(Toils_Ingest.CarryIngestibleToChewSpot), 물건이 쌓인 방에서는 방 밖 자리를 골라
        // 음식을 들고 위협 쪽으로 나갔다. 의자·서서 먹을 자리 고르기가 칸 금지를 따르므로 그 잡 동안만 지금 방 밖을 금지로 본다(못 찾으면 바닐라가 그 자리에서 먹는다).
        public static bool IsOutsideShelterMealRoom(IntVec3 cell, Pawn pawn)
        {
            if (!IsShelterMeal(pawn) || !pawn.Spawned) { return false; }

            var room = pawn.GetRoom();
            return room != null && cell.GetRoom(pawn.Map) != room;
        }
    }
}
