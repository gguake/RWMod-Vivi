using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 예약 없이 먼저 깨운 폰이 바늘을 사용한다. 나머지 작업은 바늘이 꽂힌 상태가 아니면 중단된다.
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
