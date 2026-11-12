using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
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

            if (nest.TryFindSleepTarget(pawn, out var target))
            {
                return JobMaker.MakeJob(JobDefOf.LayDown, target);
            }

            if (pawn.GetRegion() == null) { return null; }

            return base.TryGiveJob(pawn);
        }
    }
}
