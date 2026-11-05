using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 쓰러진 배고픈 구성원(아기 제외: 아기는 육아의 젖병 먹이기)에게 둥지 몫 음식을 바닐라 환자 먹이기 잡(FeedPatient)으로 먹인다.
    // 음식 고르기와 잡 만들기는 바닐라 사람 환자 먹이기 워크기버(DoctorFeedHumanlikes)의 JobOnThing을 그대로 쓴다(둥지 몫이 아닌 음식은 금지 패치가 감춘다).
    // 바닐라 워크기버의 대상 판정(HasJobOnThing)은 플레이어 세력·손님이 침대에 누운 환자만 보므로(FeedPatientUtility.ShouldBeFed) 쓰지 않는다.
    // 잡드라이버는 침대가 없어도 먹이지만 환자가 "누군가 먹여야 하는" 상태(FoodUtility.ShouldBeFedBySomeone, 같은 바닐라 판정)가 아니면 바로 끝나므로
    // 쓰러진 둥지 구성원은 그 상태로 친다(BeekeepingPatch, BeekeepingUtility.NeedsNestFeeding).
    // 장기 계측(한랭 60일): 저체온으로 쓰러진 아이가 둥지 방으로 옮겨진 뒤에도 먹여 주는 구성원이 없어 쓰러진 채 굶어 죽었다.
    public class JobGiver_ViviNestFeedPatient : ThinkNode_JobGiver
    {
        private static WorkGiver_FeedPatient _worker;

        private static WorkGiver_FeedPatient Worker
        {
            get
            {
                if (_worker == null)
                {
                    _worker = DefDatabase<WorkGiverDef>.GetNamedSilentFail("DoctorFeedHumanlikes")?.Worker as WorkGiver_FeedPatient;
                }
                return _worker;
            }
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby() || Worker == null) { return null; }

            var members = nest.lord.ownedPawns;
            for (int i = 0; i < members.Count; ++i)
            {
                var patient = members[i];
                if (patient == pawn || !patient.Spawned || !BeekeepingUtility.NeedsNestFeeding(patient) || !FeedPatientUtility.IsHungry(patient)) { continue; }
                if (!pawn.CanReserveAndReach(patient, PathEndMode.ClosestTouch, Danger.Some)) { continue; }

                var job = Worker.JobOnThing(pawn, patient);
                if (job != null) { return job; }
            }

            return null;
        }
    }
}
