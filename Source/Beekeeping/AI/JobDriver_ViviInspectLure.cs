using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class JobDriver_ViviInspectLure : JobDriver
    {
        private const int SniffTicks = 240;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            yield return Toils_General.Do(() =>
            {
                (pawn.GetLord()?.LordJob as LordJob_ViviLureScout)?.Notify_Sniffed(pawn);
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            yield return Toils_General.Wait(SniffTicks, TargetIndex.A)
                .WithEffect(() => VVEffecterDefOf.VV_Gather_Scent, TargetIndex.A)
                .WithProgressBarToilDelay(TargetIndex.A);

            yield return Toils_General.Do(() =>
            {
                var lure = job.GetTarget(TargetIndex.A).Thing as Building_ViviLure;
                if (lure == null) { return; }

                foreach (var bait in lure.BaitThings)
                {
                    bait.SplitOff(1).Destroy();
                    break;
                }
            });
        }
    }
}
