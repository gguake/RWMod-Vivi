using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 방 안의 시체를 둥지에서 먼 바깥으로 옮긴다(바닐라 HaulToCell).
    // 썩은 시체는 악취 가스를 내고 방 안에 고인 가스는 폐부패를 일으켜, 시체 하나로 로열까지 잇달아 죽었다(장기 계측).
    // 대상은 둥지 방 칸(벽이 뚫린 동안은 원래 방 칸)의 시체뿐이다(사용자 지시: 비비 집 안의 시체만). 예전에는 둥지 중심 12칸 안과 바깥 보관 칸의 시체도
    // 금지를 보지 않고 옮겨, 둥지 가까이 둔 플레이어 냉동고·무덤 비축의 시체까지 가져갈 수 있었다(플레이어 시체 보호).
    public class JobGiver_ViviNestRemoveCorpse : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }

            var corpse = FindNestRoomCorpse(pawn, nest.Territory);
            if (corpse == null || !nest.Planner.TryFindCorpseDumpCell(pawn, out var cell)) { return null; }

            // 둥지 비비에게는 둥지 몫이 아닌 물건이 금지이므로 이 잡만 금지를 보지 않는다.
            var job = JobMaker.MakeJob(JobDefOf.HaulToCell, corpse, cell);
            job.count = 1;
            job.haulMode = HaulMode.ToCellNonStorage;
            job.ignoreForbidden = true;
            return job;
        }

        // 맵 시체 목록과 둥지 방 칸 중 짧은 쪽을 훑는다. 예전에는 사고마다 맵 시체를 모두 훑어 시체가 많은 맵(수백 개)에서 수 µs였다(정적 분석 F11).
        private static Corpse FindNestRoomCorpse(Pawn pawn, ViviNestTerritory territory)
        {
            var map = pawn.Map;
            var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            if (corpses.Count == 0) { return null; }

            var roomCells = territory.OwnedRoomCells;
            if (corpses.Count <= roomCells.Count)
            {
                for (int i = 0; i < corpses.Count; ++i)
                {
                    if (corpses[i] is Corpse corpse && territory.IsNestRoomCell(corpse.Position) && CanRemove(pawn, corpse)) { return corpse; }
                }
                return null;
            }

            for (int i = 0; i < roomCells.Count; ++i)
            {
                var things = map.thingGrid.ThingsListAtFast(roomCells[i]);
                for (int j = 0; j < things.Count; ++j)
                {
                    if (things[j] is Corpse corpse && CanRemove(pawn, corpse)) { return corpse; }
                }
            }
            return null;
        }

        private static bool CanRemove(Pawn pawn, Corpse corpse)
        {
            return !corpse.IsBurning() && pawn.CanReserveAndReach(corpse, PathEndMode.ClosestTouch, Danger.Some);
        }
    }
}
