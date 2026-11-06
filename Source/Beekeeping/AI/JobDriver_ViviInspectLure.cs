using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 정찰 비비가 유인통의 꿀 냄새를 맡고 미끼를 조금 맛본다.
    public class JobDriver_ViviInspectLure : JobDriver
    {
        private const int SniffTicks = 240;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            yield return Toils_General.Do(() =>
            {
                // 길이 막혀도 다시 시도하지 않게 시작할 때 맡은 것으로 친다.
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
