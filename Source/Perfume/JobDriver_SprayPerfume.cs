using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_SprayPerfume : JobDriver
    {
        private const TargetIndex BottleIndex = TargetIndex.A;

        private CompPerfumeBottle BottleComp => job.GetTarget(BottleIndex).Thing?.TryGetComp<CompPerfumeBottle>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => BottleComp == null || BottleComp.WearingPawn != pawn || !BottleComp.IsComplete);

            yield return Toils_General.Wait(BottleComp?.Props.sprayTicks ?? 120);

            yield return Toils_General.DoAtomic(() => BottleComp?.TrySpray(pawn));
        }
    }
}
