using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 꽃 경비 듀티: 지키는 꽃이 있으면 그 곁을 배회하고(VV_TitanicHornetFlowerGuard), 꽃이 없으면 보스를 따라다닌다(VV_TitanicHornetEscort).
    // 실제 교전은 광폭 상태의 JobGiver_HornetBerserk가 지키는 곳 근처에서 처리한다.
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

            // 1페이즈가 길게 이어져도 경비 말벌이 먹이를 찾아 흩어지거나 굶어 죽지 않게 한다.
            LordToil_ApocryphaEscort.KeepFed(lord, FeedIntervalTicks);
        }

        // 지금 듀티가 지키는 곳(꽃 또는 보스)과 맞는지. 불러온 뒤 지키던 꽃이 이미 사라졌으면 듀티 초점이 낡은 채 남아 있을 수 있다.
        private static bool HasExpectedDuty(Pawn pawn, LordJob_ApocryphaFlowerGuard guardJob)
        {
            var duty = pawn.mindState.duty;
            if (duty == null || !guardJob.BossActive) { return false; }

            var flower = guardJob.GetGuardedFlower(pawn);
            return flower != null
                ? duty.def == ApocryphaDutyDefOf.VV_TitanicHornetFlowerGuard && duty.focus.Thing == flower
                : duty.def == ApocryphaDutyDefOf.VV_TitanicHornetEscort && duty.focus.Thing == guardJob.Boss;
        }

        // 초점이 비면 따라가기 작업 지급기가 오류를 내므로 보스가 없으면 습격 듀티로 둔다.
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
                ? new PawnDuty(ApocryphaDutyDefOf.VV_TitanicHornetFlowerGuard, flower, guardJob.GuardRadius)
                : new PawnDuty(ApocryphaDutyDefOf.VV_TitanicHornetEscort, guardJob.Boss, guardJob.BossFollowRadius);
        }
    }
}
