using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 비비가 요정꽃에 다가가 3초(180틱) 동안 교감한 뒤 〈요정 간섭〉을 얻는다.
    public class JobDriver_BondFairyFlower : JobDriver
    {
        public const int BondTicks = 180;

        private const TargetIndex FlowerIndex = TargetIndex.A;

        private Building_FairyFlower Flower => job.GetTarget(FlowerIndex).Thing as Building_FairyFlower;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(FlowerIndex), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(FlowerIndex);
            this.FailOn(() => Flower == null || !Building_FairyFlower.CanBond(pawn).Accepted);

            yield return Toils_Goto.GotoThing(FlowerIndex, PathEndMode.Touch);

            yield return Toils_General.Wait(BondTicks, FlowerIndex)
                .WithProgressBarToilDelay(FlowerIndex)
                .WithEffect(ApocryphaGimmickDefOf.VV_Effecter_FairyFlowerBonding, FlowerIndex);

            yield return Toils_General.DoAtomic(() => Flower?.Notify_BondedBy(pawn));
        }
    }
}
