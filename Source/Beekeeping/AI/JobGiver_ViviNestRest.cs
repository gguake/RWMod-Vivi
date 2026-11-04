using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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
            if (rest != null && pawn.GetLord()?.LordJob is LordJob_ViviNest && IsNight(pawn)
                && rest.CurLevel < NightSleepBelowLevel && Find.TickManager.TicksGame >= pawn.mindState.canSleepTick
                && RestUtility.CanFallAsleep(pawn))
            {
                return 8f;
            }

            return base.GetPriority(pawn);
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn.GetLord()?.LordJob is LordJob_ViviNest nest))
            {
                return base.TryGiveJob(pawn);
            }

            var rest = pawn.needs?.rest;
            if (rest == null || RestUtility.DisturbancePreventsLyingDown(pawn)) { return null; }

            // 아기·어린이는 둥지 부화기(아기 침대)를 쓴다. 침대는 자기 세력 것만 고른다.
            var bed = RestUtility.FindBedFor(pawn);
            if (bed != null)
            {
                return JobMaker.MakeJob(JobDefOf.LayDown, bed);
            }

            if (nest.TryFindSleepSpot(pawn, out var cell))
            {
                return JobMaker.MakeJob(JobDefOf.LayDown, cell);
            }

            return base.TryGiveJob(pawn);
        }
    }
}
