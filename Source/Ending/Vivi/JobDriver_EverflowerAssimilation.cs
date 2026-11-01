using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 〈영원꽃 완전 동화〉 동안 제단 위 영원꽃 앞 칸에 고정된 채 공중에 떠 있는다(그리기 오프셋은 헤디프가 정한다).
    // 생각 트리의 정신 상태 다음 자리(Humanlike_PostMentalState)에서 주어지므로 의식 임무·지시·작업보다 앞서며, 플레이어가 끊을 수 없다(JobDef).
    // 헤디프가 사라지면(풀림·끊어짐) 끝난다.
    public class JobDriver_EverflowerAssimilation : JobDriver
    {
        private Hediff_EverflowerAssimilation Assimilation => Hediff_EverflowerAssimilation.Of(pawn);

        public override Vector3 ForcedBodyOffset => Assimilation?.BodyOffset ?? Vector3.zero;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Assimilation == null);

            var hold = ToilMaker.MakeToil("EverflowerAssimilation");
            hold.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.Rotation = Rot4.South;
            };
            hold.tickAction = () =>
            {
                if (pawn.Rotation != Rot4.South)
                {
                    pawn.Rotation = Rot4.South;
                }
            };
            hold.handlingFacing = true;
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }
    }
}
