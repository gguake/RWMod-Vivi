using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordToil_ViviNestLive : LordToil
    {
        // 무장 비비가 지키는 둥지 둘레(바닐라 지점 방어가 이 반경 안에서 적을 찾고 사격 자리를 고른다)
        public const float DefendRadius = 16f;

        private readonly IntVec3 _nestCell;

        public override IntVec3 FlagLoc => _nestCell;

        public override bool AllowSatisfyLongNeeds => true;

        public LordToil_ViviNestLive(IntVec3 nestCell)
        {
            _nestCell = nestCell;
        }

        public override void Notify_PawnDamaged(Pawn victim, DamageInfo dinfo)
        {
            base.Notify_PawnDamaged(victim, dinfo);
            (lord.LordJob as LordJob_ViviNest)?.Hostility.Notify_MemberDamaged(victim, dinfo);
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; ++i)
            {
                lord.ownedPawns[i].mindState.duty = new PawnDuty(VVDutyDefOf.VV_ViviNestLife, _nestCell, DefendRadius);
            }
        }
    }
}
