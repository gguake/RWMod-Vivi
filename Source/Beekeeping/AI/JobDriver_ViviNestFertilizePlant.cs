using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 바닐라 연료 보충(JobDriver_Refuel)과 같은 순서: 둥지 몫 비료를 집어(모자라면 가까운 둥지 몫 비료를 더 집는다) 마법식물에 가서 잠시 일한 뒤 저장 마나로 넣는다.
    // 바닐라처럼 가득 찰 만큼만 쓰고, 남은 비료는 잡이 끝날 때 내려놓아 둥지 몫으로 남는다.
    public class JobDriver_ViviNestFertilizePlant : JobDriver
    {
        private const TargetIndex PlantInd = TargetIndex.A;
        private const TargetIndex FertilizerInd = TargetIndex.B;
        // 바닐라 연료 보충과 같은 시간
        private const int FertilizingDuration = 240;

        // 비료 하나의 마나: 마나 방출기는 비료를 하루 8개 태워(fuelConsumptionRate 8, fuelMultiplier 1) 하루 800 마나를 낸다(ManaFluxRule_Refuelable).
        // 같은 비율로 넣으므로 비료로 마나를 더 만들지 않는다. 방출기 마나는 환경 격자로 퍼지며 줄고 식물은 자기 칸 것만 흡수하므로, 손으로 주면 덜 흩어질 뿐이다.
        public const float ManaPerFertilizer = 100f;

        private ArcanePlant Plant => (ArcanePlant)job.GetTarget(PlantInd).Thing;
        private Thing Fertilizer => job.GetTarget(FertilizerInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Plant, job, errorOnFailed: errorOnFailed) && pawn.Reserve(Fertilizer, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(PlantInd);
            AddEndCondition(() => Plant.ManaComp.StoredPct < 1f ? JobCondition.Ongoing : JobCondition.Succeeded);

            yield return Toils_General.DoAtomic(() => job.count = FertilizerCountToFill(Plant.ManaComp));
            var reserveFertilizer = Toils_Reserve.Reserve(FertilizerInd);
            yield return reserveFertilizer;
            yield return Toils_Goto.GotoThing(FertilizerInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(FertilizerInd).FailOnSomeonePhysicallyInteracting(FertilizerInd);
            yield return Toils_Haul.StartCarryThing(FertilizerInd, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(FertilizerInd);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveFertilizer, FertilizerInd, TargetIndex.None, takeFromValidStorage: true, extraValidator: IsUsableDuplicate);
            yield return Toils_Goto.GotoThing(PlantInd, PathEndMode.Touch);
            yield return Toils_General.Wait(FertilizingDuration)
                .FailOnDestroyedNullOrForbidden(FertilizerInd)
                .FailOnDestroyedNullOrForbidden(PlantInd)
                .FailOnCannotTouch(PlantInd, PathEndMode.Touch)
                .WithProgressBarToilDelay(PlantInd);
            yield return Toils_General.Do(Fertilize);
        }

        // 근처 비료를 더 집을 때도 둥지 몫만, 밤·위험한 날씨에는 방 안 것만 집는다.
        private bool IsUsableDuplicate(Thing thing)
        {
            var nest = pawn.GetNestLordJob();
            return nest != null && JobGiver_ViviNestFertilizePlant.IsUsableFertilizer(thing, pawn, nest, JobGiver_ViviNestFertilizePlant.IndoorOnly(pawn));
        }

        private void Fertilize()
        {
            var mana = Plant.ManaComp;
            var fertilizer = Fertilizer;
            var count = Mathf.Min(FertilizerCountToFill(mana), fertilizer.stackCount);
            mana.Stored += count * ManaPerFertilizer;
            fertilizer.SplitOff(count).Destroy();

            var nest = pawn.GetNestLordJob();
            if (nest != null)
            {
                nest.Stock.MarkDirty();
                nest.Production.Notify_PlantFertilized(count);
            }
        }

        // 바닐라 연료 보충(CompRefuelable.GetFuelCountToFullyRefuel)처럼 가득 찰 만큼 올림으로 센다.
        public static int FertilizerCountToFill(CompMana mana)
        {
            return Mathf.Max(Mathf.CeilToInt((mana.Props.manaCapacity - mana.Stored) / ManaPerFertilizer), 1);
        }
    }
}
