using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class LordToil_ApocryphaEscort : LordToil_ApocryphaHornetGuard
    {
        private LordJob_ApocryphaEscort EscortJob => lord.LordJob as LordJob_ApocryphaEscort;

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

            KeepFed();
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
    }
}
