using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 둥지를 떠날 때(평화 이주·적대 도주) 어른은 아기를 안고 나가고, 나머지는 바닐라 맵 나가기로 나간다.
    // 아기는 스스로 걷지 못해 바닐라 맵 나가기 토일로는 맵에 남고 로드가 끝나지 않는다.
    public class LordToil_ViviNestExit : LordToil_ViviNest
    {
        private readonly LocomotionUrgency _locomotion;
        private readonly bool _canDig;

        public override bool AllowSatisfyLongNeeds => false;

        public LordToil_ViviNestExit(LocomotionUrgency locomotion, bool canDig)
        {
            _locomotion = locomotion;
            _canDig = canDig;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; ++i)
            {
                var duty = new PawnDuty(VVDutyDefOf.VV_ViviNestExit)
                {
                    locomotion = _locomotion,
                    canDig = _canDig,
                };
                lord.ownedPawns[i].mindState.duty = duty;
            }
        }
    }
}
