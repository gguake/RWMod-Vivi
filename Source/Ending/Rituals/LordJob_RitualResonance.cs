using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 앵커 영원꽃에서 시작한 의식(조율·마나 파동·요정화 등)의 로드. 세 행동 워커가 앵커 대상일 때 이 로드를 만든다.
    // 보스전(1·2페이즈) 동안에는 아포크리파가 코앞까지 다가와도 참가자가 의식을 포기하지 않도록, 바닐라 취소 조건에서 도주 판정과
    // 다른 폰(아포크리파·채집자 등)의 영원꽃 예약 판정을 뺀다.
    // 보스전 여부는 생성 시점이 아니라 매 판정마다 확인한다(평화 기간에 시작한 의식도 보스가 등장하면 취소되지 않는다).
    public class LordJob_RitualResonance : LordJob_Ritual
    {
        // 저장/불러오기용(Lord가 LordJob을 Deep 저장한다). 추가 저장 필드는 없다.
        public LordJob_RitualResonance() { }

        public LordJob_RitualResonance(TargetInfo selectedTarget, Precept_Ritual ritual, RitualObligation obligation, List<RitualStage> allStages, RitualRoleAssignments assignments, Pawn organizer = null, IntVec3? spotOverride = null)
            : base(selectedTarget, ritual, obligation, allStages, assignments, organizer, spotOverride)
        {
        }

        protected override bool ShouldBeCalledOff()
        {
            if (!EverflowerRitualRules.IsAnchorUnderBossFight(selectedTarget.Thing as ArcanePlant_Everflower))
            {
                return base.ShouldBeCalledOff();
            }

            // 아래는 LordJob_Ritual.ShouldBeCalledOff와 같고, 마지막의 SelfDefenseUtility.ShouldStartFleeing 검사만 없다.
            // 대상 검사는 바닐라 TargetStillAllowed()에서 다른 폰의 예약 검사(ChecksReservations)만 뺀 것이다.
            if (lord.ownedPawns.Count == 0) { return true; }
            if (selectedTarget.ThingDestroyed) { return true; }
            if (!Ritual.behavior.TargetStillAllowed(selectedTarget, this)) { return true; }
            if (organizer != null && organizer.Downed) { return true; }

            foreach (var role in assignments.AllRolesForReading)
            {
                if (!role.required) { continue; }

                var pawn = assignments.FirstAssignedPawn(role);
                if (pawn != null && !lord.ownedPawns.Contains(pawn) && ShouldCallOffBecausePawnNoLongerOwned(pawn))
                {
                    return true;
                }
            }

            var extraRequiredPawns = assignments.ExtraRequiredPawnsForReading;
            if (extraRequiredPawns != null)
            {
                foreach (var pawn in extraRequiredPawns)
                {
                    if (!lord.ownedPawns.Contains(pawn) && ShouldCallOffBecausePawnNoLongerOwned(pawn))
                    {
                        return true;
                    }

                    var holder = (pawn.Corpse?.ParentHolder != null) ? pawn.Corpse.ParentHolder.ParentHolder : pawn.CarriedBy;
                    if (holder is Pawn carrier && carrier.GetLord() != lord)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // 2페이즈 공명 파동(조율 의식)의 역할 참가자는 출혈 중에도 의식 자리를 떠나지 않는다.
        // 역할 def의 출혈 무시는 모든 조율 의식에 걸리므로 XML에 두지 않고, 바닐라 판정 동안만 잠시 켠다.
        public override float VoluntaryJoinPriorityFor(Pawn p)
        {
            var role = RoleFor(p, includeForced: true);
            if (!EverflowerRitualRules.ShouldIgnoreBleeding(role, Ritual, selectedTarget.Thing as ArcanePlant_Everflower))
            {
                return base.VoluntaryJoinPriorityFor(p);
            }

            role.ignoreBleeding = true;
            try
            {
                return base.VoluntaryJoinPriorityFor(p);
            }
            finally
            {
                role.ignoreBleeding = false;
            }
        }
    }
}
