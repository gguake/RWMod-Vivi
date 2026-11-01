using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
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
                // 호위 대상이 없으면 따라가기 작업에서 오류가 나므로 보스 부재 시 습격 듀티를 사용한다.
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

                if (focus == null || !TryEngageFocus(pawn, focus))
                {
                    TryEngageImmediately(pawn);
                }
            }

            // 장기전에서도 호위 말벌이 먹이를 찾아 이탈하거나 굶어 죽지 않게 한다.
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

        // 따라가기·배회는 완료 전까지 생각 트리를 재평가하지 않아 적의 접근에 늦게 반응한다.
        // 대기 중인 말벌은 짧은 주기로 대상을 확인하고 발견 즉시 작업을 다시 선택한다.
        internal static void TryEngageImmediately(Pawn pawn)
        {
            if (!CanEngage(pawn)) { return; }

            var curJob = pawn.CurJob;
            if (curJob == null || curJob.jobGiver is JobGiver_HornetBerserk) { return; }

            // 주변에 공격 후보가 없으면 영역을 두 번 탐색하는 정식 대상 탐색은 생략한다.
            if (pawn.GetLord()?.LordJob is LordJob_ApocryphaHornetGuard guardJob && !guardJob.AnyPotentialTargetNear(pawn)) { return; }

            if (JobGiver_HornetBerserk.HasAttackTarget(pawn))
            {
                pawn.jobs.CheckForJobOverride();
            }
        }

        // 근접 공격은 대상이 다르면 기존 작업을 이어가지 않으므로 재평가 시 새 적을 선택한다.
        // 도달 불가한 적은 작업 지급기도 선택하지 않으므로 재평가 없이 false를 반환한다.
        internal static bool TryEngageFocus(Pawn pawn, Thing focus)
        {
            if (!CanEngage(pawn) || focus == null) { return false; }

            var curJob = pawn.CurJob;
            if (curJob == null || (curJob.def == JobDefOf.AttackMelee && curJob.targetA.Thing == focus)) { return true; }
            if (!pawn.CanReach(focus, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, canBashFences: pawn.def.race.FenceBlocked)) { return false; }

            pawn.jobs.CheckForJobOverride();
            return true;
        }

        private static bool CanEngage(Pawn pawn)
        {
            return pawn.Spawned && !pawn.Downed && pawn.MentalStateDef == VVMentalStateDefOf.VV_HornetBerserk;
        }
    }
}
