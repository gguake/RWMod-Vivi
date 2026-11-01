using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 2페이즈 바닥에 꽂힌 거대 엘텍스 바늘에 다가가 잠시(unboundNeedleActivateTicks) 손을 대 깨운다. 깨어난 바늘은 이 폰을 뒤쫓으며 겨눈다.
    // 바늘은 예약하지 않는다(먼저 깨운 폰이 있으면 나머지는 바늘이 더 이상 꽂혀 있지 않아 그만둔다).
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

            // 꽂힌 바늘의 칸(Position)은 촉이 박힌 자리다.
            yield return Toils_Goto.GotoThing(NeedleIndex, PathEndMode.Touch);

            yield return Toils_General.Wait(Needle?.ActivateTicks ?? 60, NeedleIndex)
                .WithProgressBarToilDelay(NeedleIndex);

            yield return Toils_General.DoAtomic(() => Needle?.Activate(pawn));
        }
    }
}
