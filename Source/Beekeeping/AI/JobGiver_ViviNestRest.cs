using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 일과표가 없는 NPC는 피곤할 때만 자므로, 둥지 비비는 밤에 자고 둥지 방 안에서 잠자리를 고르게 한다.
    public class JobGiver_ViviNestRest : JobGiver_GetRest
    {
        private const int NightStartHour = 22;
        private const int NightEndHour = 6;
        private const float NightSleepBelowLevel = 0.95f;

        public static bool IsNight(Pawn pawn)
        {
            var hour = GenLocalDate.HourOfDay(pawn);
            return hour >= NightStartHour || hour < NightEndHour;
        }

        public override float GetPriority(Pawn pawn)
        {
            var rest = pawn.needs?.rest;
            if (rest != null && pawn.GetNestLordJob() != null && IsNight(pawn)
                && rest.CurLevel < NightSleepBelowLevel && Find.TickManager.TicksGame >= pawn.mindState.canSleepTick
                && RestUtility.CanFallAsleep(pawn))
            {
                return 8f;
            }

            return base.GetPriority(pawn);
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null)
            {
                return base.TryGiveJob(pawn);
            }

            var rest = pawn.needs?.rest;
            if (rest == null || RestUtility.DisturbancePreventsLyingDown(pawn)) { return null; }

            // 침대는 자기 세력 것만 고르고, 둥지 부화기(아기 침대)는 알과 아기 몫이라 어린이는 둥지 방 바닥에서 잔다(LordJob_ViviNest.FindBedFor).
            if (nest.TryFindSleepTarget(pawn, out var target))
            {
                return JobMaker.MakeJob(JobDefOf.LayDown, target);
            }

            // 막 완성된 벽 칸처럼 영역이 없는 칸에 서 있으면 바닐라 땅바닥 잠자리 찾기가 예외를 내므로, 이동 복구를 기다린다.
            if (pawn.GetRegion() == null) { return null; }

            return base.TryGiveJob(pawn);
        }
    }
}
