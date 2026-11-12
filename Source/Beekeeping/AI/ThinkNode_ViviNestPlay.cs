using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ThinkNode_ViviNestPlay : ThinkNode_Random
    {
        private const float PlayChance = 0.35f;
        public const int RethinkTicks = 600;

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return ThinkResult.NoJob; }
            if (JobGiver_ViviNestRest.IsNight(pawn)) { return ThinkResult.NoJob; }
            if (pawn.IsRoyalVivi() && HasBaby(nest)) { return ThinkResult.NoJob; }

            var giver = pawn.CurJob?.jobGiver;
            if (giver != null && subNodes.Contains(giver))
            {
                var result = giver.TryIssueJobPackage(pawn, jobParams);
                if (result.IsValid) { return result; }
            }

            if (!Rand.Chance(PlayChance)) { return ThinkResult.NoJob; }
            return base.TryIssueJobPackage(pawn, jobParams);
        }

        public static Job CurrentPlay(Pawn pawn, ThinkNode giver)
        {
            var job = pawn.CurJob;
            return job != null && job.jobGiver == giver ? job : null;
        }

        public static int Elapsed(Job job) => Find.TickManager.TicksGame - job.startTick;

        public static void SetRethink(Job job)
        {
            job.expiryInterval = RethinkTicks;
            job.checkOverrideOnExpire = true;
        }

        public static bool IsOutdoorPlaySafe(Pawn pawn, IntVec3 cell)
        {
            return TryGetOutdoorPlayRange(pawn, out var comfortable) && comfortable.Includes(GenTemperature.GetTemperatureForCell(cell, pawn.Map));
        }

        public static bool IsOutdoorPlayWeatherSafe(Pawn pawn, out FloatRange comfortable)
        {
            return TryGetOutdoorPlayRange(pawn, out comfortable) && comfortable.Includes(pawn.Map.mapTemperature.OutdoorTemp);
        }

        private static bool TryGetOutdoorPlayRange(Pawn pawn, out FloatRange comfortable)
        {
            comfortable = default;
            var map = pawn.Map;
            if (map == null || BeekeepingUtility.OutdoorDangerous(map)) { return false; }
            if (pawn.health.hediffSet.HasTemperatureInjury(TemperatureInjuryStage.Initial)) { return false; }

            comfortable = pawn.ComfortableTemperatureRange();
            return true;
        }

        private static bool HasBaby(LordJob_ViviNest nest)
        {
            var pawns = nest.lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                if (pawns[i].DevelopmentalStage.Baby()) { return true; }
            }
            return false;
        }
    }
}
