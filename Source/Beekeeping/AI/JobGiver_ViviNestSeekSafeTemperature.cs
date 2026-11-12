using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestSeekSafeTemperature : ThinkNode_JobGiver
    {
        private const int WaitTicks = 500;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }
            if (!pawn.health.hediffSet.HasTemperatureInjury(TemperatureInjuryStage.Minor)) { return null; }

            var rooms = nest.ComfortableRooms(pawn);
            var room = pawn.GetRoom();
            if (pawn.ComfortableTemperatureRange().Includes(pawn.AmbientTemperature) || (room != null && rooms.Contains(room)))
            {
                return JobMaker.MakeJob(JobDefOf.Wait_SafeTemperature, WaitTicks, checkOverrideOnExpiry: true);
            }

            return nest.TryFindRoomCell(pawn, rooms, out var cell) ? JobMaker.MakeJob(JobDefOf.GotoSafeTemperature, cell) : null;
        }
    }
}
