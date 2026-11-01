using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 1페이즈에는 근접 공격이 없으므로 인접한 적을 자동 공격하는 바닐라 대기를 사용하지 않는다.
    // 바닐라 만료 검사는 해시 간격만큼 늦을 수 있어 job.expiryInterval 길이의 지연 토일로 대기를 끝낸다.
    public class JobDriver_ApocryphaWait : JobDriver
    {
        private const int DefaultWaitTicks = 60;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var toil = ToilMaker.MakeToil("ApocryphaWait")
                .WithInitAction(() =>
                {
                    pawn.pather?.StopDead();
                    Map.pawnDestinationReservationManager.Reserve(pawn, job, pawn.Position);
                })
                .WithDefaultCompleteMode(ToilCompleteMode.Delay);
            toil.defaultDuration = job.expiryInterval > 0 ? job.expiryInterval : DefaultWaitTicks;

            yield return toil;
        }
    }
}
