using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ViviNestFertilizePlant : JobDriver
    {
        private const TargetIndex PlantInd = TargetIndex.A;
        private const TargetIndex FertilizerInd = TargetIndex.B;
        
        private const int FertilizingDuration = 240;
        private const float ManaPerFertilizer = 100f;

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

        private bool IsUsableDuplicate(Thing thing)
        {
            var nest = pawn.GetNestLordJob();
            return nest != null && JobGiver_ViviNestFertilizePlant.IsUsableFertilizer(thing, pawn, nest, BeekeepingUtility.OutdoorWorkPaused(pawn));
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

        public static int FertilizerCountToFill(CompMana mana)
        {
            return Mathf.Max(Mathf.CeilToInt((mana.Props.manaCapacity - mana.Stored) / ManaPerFertilizer), 1);
        }
    }
}
