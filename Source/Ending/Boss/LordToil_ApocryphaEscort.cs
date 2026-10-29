using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 호위 듀티(VV_TitanicHornetEscort): 보스를 따라다니며 근처를 지킨다. 실제 교전은 광폭 상태의 JobGiver_HornetBerserk가 보스 반경 안에서 처리한다.
    public class LordToil_ApocryphaEscort : LordToil
    {
        private const int FeedIntervalTicks = 2500;
        private const int ReactionCheckIntervalTicks = 30;

        private LordJob_ApocryphaEscort EscortJob => lord.LordJob as LordJob_ApocryphaEscort;

        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            var escortJob = EscortJob;
            var boss = escortJob?.Boss;
            foreach (var pawn in lord.ownedPawns)
            {
                // 초점이 비면 따라가기 작업 지급기가 오류를 내므로 보스가 없으면 습격 듀티로 둔다.
                pawn.mindState.duty = escortJob != null && escortJob.BossActive
                    ? new PawnDuty(ApocryphaDutyDefOf.VV_TitanicHornetEscort, boss, escortJob.GuardRadius)
                    : new PawnDuty(VVDutyDefOf.VV_TitanicHornetAssault);
            }
        }

        public override void LordToilTick()
        {
            base.LordToilTick();

            var focus = EscortJob?.FocusTarget;
            for (int i = lord.ownedPawns.Count - 1; i >= 0; i--)
            {
                var pawn = lord.ownedPawns[i];
                if (!pawn.IsHashIntervalTick(ReactionCheckIntervalTicks)) { continue; }

                if (focus != null)
                {
                    TryEngageFocus(pawn, focus);
                }
                else
                {
                    TryEngageImmediately(pawn);
                }
            }

            // 1페이즈가 길게 이어져도 호위 말벌이 먹이를 찾아 흩어지거나 굶어 죽지 않게 한다.
            KeepFed(lord, FeedIntervalTicks);
        }

        internal static void KeepFed(Lord lord, int intervalTicks)
        {
            if (Find.TickManager.TicksGame % intervalTicks != 0) { return; }

            foreach (var pawn in lord.ownedPawns)
            {
                var food = pawn.needs?.food;
                if (food != null)
                {
                    food.CurLevelPercentage = 1f;
                }
            }
        }

        // 따라가기·배회 작업은 끝날 때까지(길면 수 초) 생각 트리를 다시 보지 않아 적이 다가와도 늦게 반응한다.
        // 교전 중이 아닌 말벌은 짧은 주기로 대상을 찾아, 있으면 바로 작업을 다시 판단시킨다.
        internal static void TryEngageImmediately(Pawn pawn)
        {
            if (!CanEngage(pawn)) { return; }

            var curJob = pawn.CurJob;
            if (curJob == null || curJob.jobGiver is JobGiver_HornetBerserk) { return; }

            if (JobGiver_HornetBerserk.HasAttackTarget(pawn))
            {
                pawn.jobs.CheckForJobOverride();
            }
        }

        // 보스가 감지한 적이 있으면 그 적을 공격하고 있지 않은 말벌(다른 대상과 교전 중인 말벌 포함)을 곧바로 그 적에게 돌린다.
        // (근접 공격 작업은 대상이 다르면 이어지는 작업으로 보지 않으므로 다시 판단하면 새 대상으로 바뀐다)
        internal static void TryEngageFocus(Pawn pawn, Thing focus)
        {
            if (!CanEngage(pawn) || focus == null) { return; }

            var curJob = pawn.CurJob;
            if (curJob == null || (curJob.def == JobDefOf.AttackMelee && curJob.targetA.Thing == focus)) { return; }

            pawn.jobs.CheckForJobOverride();
        }

        private static bool CanEngage(Pawn pawn)
        {
            return pawn.Spawned && !pawn.Downed && pawn.MentalStateDef == VVMentalStateDefOf.VV_HornetBerserk;
        }
    }
}
