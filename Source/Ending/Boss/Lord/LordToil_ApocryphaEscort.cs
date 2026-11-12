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
                pawn.mindState.duty = escortJob != null && escortJob.BossActive
                    ? new PawnDuty(VVDutyDefOf.VV_TitanicHornetEscort, boss, escortJob.GuardRadius)
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

        internal static void TryEngageImmediately(Pawn pawn)
        {
            if (!CanEngage(pawn)) { return; }

            var curJob = pawn.CurJob;
            if (curJob == null || curJob.jobGiver is JobGiver_HornetBerserk) { return; }

            if (pawn.GetLord()?.LordJob is LordJob_ApocryphaHornetGuard guardJob && !guardJob.AnyPotentialTargetNear(pawn)) { return; }

            if (JobGiver_HornetBerserk.TryPrepareAttackJob(pawn))
            {
                pawn.jobs.CheckForJobOverride();
            }
        }

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
