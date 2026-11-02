using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobDriver_ActivateApocryphaNeedle : JobDriver
    {
        private const TargetIndex NeedleIndex = TargetIndex.A;

        private ApocryphaNeedle Needle => job.GetTarget(NeedleIndex).Thing as ApocryphaNeedle;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(NeedleIndex);
            this.FailOn(() => Needle == null || !Needle.CanActivate);

            yield return Toils_Goto.GotoThing(NeedleIndex, PathEndMode.Touch);

            yield return Toils_General.Wait(Needle?.ActivateTicks ?? 60, NeedleIndex)
                .WithProgressBarToilDelay(NeedleIndex);

            yield return Toils_General.DoAtomic(() => Needle?.Activate(pawn));
        }
    }
}
