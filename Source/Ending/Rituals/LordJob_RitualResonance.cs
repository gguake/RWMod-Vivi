using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 평화 기간에 시작한 의식도 보스 등장 후 보호되도록 보스전 여부를 판정할 때마다 확인한다.
    public class LordJob_RitualResonance : LordJob_Ritual
    {
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

            // 바닐라 LordJob_Ritual.ShouldBeCalledOff에서 도주 판정(SelfDefenseUtility.ShouldStartFleeing)만 제외한다.
            // 대상 검사도 TargetStillAllowed의 다른 폰 예약 검사(ChecksReservations)만 제외한다.
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

        // 일반 조율에도 영향을 주는 XML 설정 대신 바닐라 판정 중에만 출혈 무시를 임시로 켠다.
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
