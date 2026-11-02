using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class LordToil_ApocryphaFlowerGuard : LordToil
    {
        private const int FeedIntervalTicks = 2500;
        private const int ReactionCheckIntervalTicks = 30;
        private const int GuardRefreshIntervalTicks = 120;

        private LordJob_ApocryphaFlowerGuard GuardJob => lord.LordJob as LordJob_ApocryphaFlowerGuard;

        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            foreach (var pawn in lord.ownedPawns)
            {
                GuardJob?.RefreshGuardedFlower(pawn);
                AssignDuty(pawn);
            }
        }

        public override void LordToilTick()
        {
            base.LordToilTick();

            var guardJob = GuardJob;
            for (int i = lord.ownedPawns.Count - 1; i >= 0; i--)
            {
                var pawn = lord.ownedPawns[i];
                if (guardJob != null && pawn.IsHashIntervalTick(GuardRefreshIntervalTicks) && (guardJob.RefreshGuardedFlower(pawn) || !HasExpectedDuty(pawn, guardJob)))
                {
                    AssignDuty(pawn);
                }

                if (pawn.IsHashIntervalTick(ReactionCheckIntervalTicks))
                {
                    LordToil_ApocryphaEscort.TryEngageImmediately(pawn);
                }
            }

            LordToil_ApocryphaEscort.KeepFed(lord, FeedIntervalTicks);
        }

        // 불러온 뒤 지키던 꽃이 사라졌을 수 있으므로 듀티 대상이 현재 경비 대상과 일치하는지 확인한다.
        private static bool HasExpectedDuty(Pawn pawn, LordJob_ApocryphaFlowerGuard guardJob)
        {
            var duty = pawn.mindState.duty;
            if (duty == null || !guardJob.BossActive) { return false; }

            var flower = guardJob.GetGuardedFlower(pawn);
            return flower != null
                ? duty.def == VVDutyDefOf.VV_TitanicHornetFlowerGuard && duty.focus.Thing == flower
                : duty.def == VVDutyDefOf.VV_TitanicHornetEscort && duty.focus.Thing == guardJob.Boss;
        }

        // 호위 대상이 없으면 따라가기 작업에서 오류가 나므로 보스 부재 시 습격 듀티를 사용한다.
        private void AssignDuty(Pawn pawn)
        {
            var guardJob = GuardJob;
            if (guardJob == null || !guardJob.BossActive)
            {
                pawn.mindState.duty = new PawnDuty(VVDutyDefOf.VV_TitanicHornetAssault);
                return;
            }

            var flower = guardJob.GetGuardedFlower(pawn);
            pawn.mindState.duty = flower != null
                ? new PawnDuty(VVDutyDefOf.VV_TitanicHornetFlowerGuard, flower, guardJob.GuardRadius)
                : new PawnDuty(VVDutyDefOf.VV_TitanicHornetEscort, guardJob.Boss, guardJob.BossFollowRadius);
        }
    }
}
