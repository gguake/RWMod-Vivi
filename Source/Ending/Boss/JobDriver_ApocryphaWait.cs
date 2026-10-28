using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 아포크리파가 제자리에 멈춰 서는 작업. 바닐라 대기와 달리 인접한 적을 자동으로 공격하지 않는다(1페이즈에는 근접 공격이 없음).
    // 대기 시간은 job.expiryInterval로 받는다. 바닐라 만료 판정은 해시 간격에 맞춰서만 일어나 최대 한 간격만큼 늦어지므로
    // 같은 길이의 지연 토일로 정확히 끝내고, 만료는 안전장치로만 남긴다.
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
