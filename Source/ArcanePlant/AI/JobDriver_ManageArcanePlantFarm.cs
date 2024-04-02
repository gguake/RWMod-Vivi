using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ManageArcanePlantFarm : JobDriver
    {
        public const int ManageTick = 3000;

        private const TargetIndex FarmIdx = TargetIndex.A;

        protected Building_ArcanePlantFarm Farm => job.GetTarget(FarmIdx).Thing as Building_ArcanePlantFarm;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(FarmIdx);
            AddFailCondition(() => Farm.Bill == null || Farm.Bill.Stage != GrowingArcanePlantBillStage.Growing || !Farm.Bill.ManagementRequired);

            yield return Toils_Goto.GotoThing(FarmIdx, PathEndMode.Touch);

            var ticks = Mathf.CeilToInt(ManageTick / Mathf.Pow(Mathf.Max(0.01f, pawn.GetStatValue(StatDefOf.PlantWorkSpeed)), 2f));
            yield return Toils_General.WaitWith(FarmIdx, ticks, useProgressBar: true, face: FarmIdx)
                .FailOnDestroyedNullOrForbidden(FarmIdx);

            yield return ToilMaker.MakeToil()
                .WithDefaultCompleteMode(ToilCompleteMode.Instant)
                .WithInitAction(() =>
                {
                    Farm.Bill.Manage();
                });
        }
    }
}
