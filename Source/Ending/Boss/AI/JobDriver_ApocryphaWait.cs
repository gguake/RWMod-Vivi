using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
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
