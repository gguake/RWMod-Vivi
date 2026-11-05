using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 저체온·열사병이 오면 편한 기온의 둥지 방으로 가서 나을 때까지 머문다(바닐라 정착민의 안전 기온 찾기와 같은 잡).
    // 바닐라 JobGiver_SeekSafeTemperature는 정착민 사고 트리에만 있고 아무 방(플레이어 방 포함)에서나 찾으므로 둥지 방에서만 고른다.
    // 바닐라는 심한 단계부터 피하지만 동상도 심한 단계부터 생기므로, 겨울에도 바깥 일·놀이를 하는 둥지 비비는 가벼운 단계부터 피한다.
    // 장기 계측(온대림 겨울 -15°C): 피할 곳을 찾지 않아 잔불꽃이 없는 증축 방에서 지내던 아이가 동상으로 죽었다.
    public class JobGiver_ViviNestSeekSafeTemperature : ThinkNode_JobGiver
    {
        private const int WaitTicks = 500;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (!pawn.health.hediffSet.HasTemperatureInjury(TemperatureInjuryStage.Minor)) { return null; }

            // 편한 기온이거나, 이보다 나은 둥지 방이 없으면 그 자리에서 나을 때까지 기다린다.
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
