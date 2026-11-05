using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 쓰러진 구성원을 편한 기온의 둥지 방으로 옮긴다(바닐라 운반 잡 HaulToCell, 시체 치우기와 같다). 치료는 옮긴 뒤 그 자리에서 한다.
    // 바닐라 구조 잡(Rescue)은 침대로만 옮기는데 둥지에는 어른 침대가 없다. 옮길지는 바닐라 구조 판정(HealthAIUtility.CanRescueNow)을 따른다(가까이 적이 있으면 미룬다).
    // 아기는 바닐라 육아(위험에서 데려오기)가 옮긴다.
    // 장기 계측(툰드라 겨울 -29°C): 맹수와 싸우다 바깥에서 쓰러진 새총잡이가 그 자리에서 치료만 받다 얼어 죽었다.
    public class JobGiver_ViviNestRescue : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }

            var members = nest.lord.ownedPawns;
            for (int i = 0; i < members.Count; ++i)
            {
                var patient = members[i];
                if (patient == pawn || !patient.Spawned || patient.DevelopmentalStage.Baby() || !patient.Downed) { continue; }

                // 이미 가장 나은 둥지 방에 있으면 그대로 둔다.
                var rooms = nest.ComfortableRooms(patient);
                var room = patient.GetRoom();
                if (room != null && rooms.Contains(room)) { continue; }
                if (!HealthAIUtility.CanRescueNow(pawn, patient) || !nest.TryFindRoomCell(pawn, rooms, out var cell)) { continue; }

                var job = JobMaker.MakeJob(JobDefOf.HaulToCell, patient, cell);
                job.count = 1;
                job.haulMode = HaulMode.ToCellNonStorage;
                return job;
            }

            return null;
        }
    }
}
