using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 놀이 묶음의 낮잠: 졸리면(바닐라 잠들기 조건, 휴식 75% 미만) 밤잠과 같은 자리 고르기(편한 기온의 둥지 방 바닥, 자기 세력 침대가 있으면 침대)로
    // 낮에 잠깐 잔다(바닐라 눕기 잡). 깨거나 낮잠 시간이 지나면 끝나고, 눕기 잡은 211틱마다 다시 생각하므로 일이 생기면 바로 일어난다.
    public class JobGiver_ViviNestNap : ThinkNode_JobGiver
    {
        // 낮잠 시간(바닐라 잡 만료, 만료 간격 단위로 확인하므로 실제로는 이것의 1~2배)
        private static readonly IntRange NapTicks = new IntRange(1250, 2000);
        // 잠자리까지 걸어가 잠들 때까지 기다리는 시간. 그 뒤로 깨어 있으면 다른 놀이를 고른다.
        private const int FallAsleepTicks = 600;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || pawn.needs?.rest == null) { return null; }

            var current = ThinkNode_ViviNestPlay.CurrentPlay(pawn, this);
            if (current != null)
            {
                return pawn.jobs.curDriver.asleep || ThinkNode_ViviNestPlay.Elapsed(current) < FallAsleepTicks ? current : null;
            }

            if (!RestUtility.CanFallAsleep(pawn) || RestUtility.DisturbancePreventsLyingDown(pawn)) { return null; }

            Job job;
            var bed = RestUtility.FindBedFor(pawn);
            if (bed != null)
            {
                job = JobMaker.MakeJob(VVJobDefOf.VV_ViviNestNap, bed);
            }
            else if (nest.TryFindSleepSpot(pawn, out var cell))
            {
                job = JobMaker.MakeJob(VVJobDefOf.VV_ViviNestNap, cell);
            }
            else
            {
                return null;
            }

            job.expiryInterval = NapTicks.RandomInRange;
            // 바닐라 눕기 잡은 잡 정의의 보고 문자열 대신 자는 중·쉬는 중을 보이므로 낮잠 보고 문자열을 덮어 쓴다.
            job.reportStringOverride = job.def.reportString;
            return job;
        }
    }
}
