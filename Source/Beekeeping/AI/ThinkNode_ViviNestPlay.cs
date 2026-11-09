using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 놀이 묶음(사용자 요구 "편자 막대 놀이를 '놀이' 중 하나로 묶고 낮잠자기·빈둥거리기를 더한다"): 한가할 때(위의 일 노드가 모두 잡을 못 줄 때)
    // 편자 막대 놀이·낮잠·빈둥거리기 중 하나를 바닐라 ThinkNode_Random처럼 섞어 고르고, 고르지 못하면 산책으로 넘어간다.
    // 하던 놀이는 놀이 시간 동안 이어 한다(그 잡 기버가 지금 잡을 다시 주면 바닐라가 그대로 둔다). 섞어 고르기만 하면 낮잠(바닐라 눕기 잡은 211틱마다
    // 다시 생각한다)이 금세 다른 놀이로 바뀐다. 놀이 잡은 자주 다시 생각해(RethinkTicks) 일이 생기면 바로 넘어간다.
    // 놀이는 낮에만 하고, 아기가 있으면 로열은 놀지 않는다(육아가 급할 때 바로 받게).
    public class ThinkNode_ViviNestPlay : ThinkNode_Random
    {
        // 생각할 때마다 새 놀이를 시작할 확률. 못 하면 산책한다(예전 편자 놀이와 같다).
        private const float PlayChance = 0.35f;
        // 놀이 잡을 다시 생각하는 간격(바닐라 잡 만료 후 재검토). 예전 편자 놀이는 2000틱마다 다시 생각해 일이 생겨도 오래 놀았다.
        public const int RethinkTicks = 600;

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return ThinkResult.NoJob; }
            if (JobGiver_ViviNestRest.IsNight(pawn)) { return ThinkResult.NoJob; }
            if (pawn.IsRoyalVivi() && HasBaby(nest)) { return ThinkResult.NoJob; }

            // 하던 놀이를 먼저 이어 한다. 놀이 시간이 지났거나 더 할 수 없으면 그 잡 기버가 잡을 주지 않아 새로 고른다.
            var giver = pawn.CurJob?.jobGiver;
            if (giver != null && subNodes.Contains(giver))
            {
                var result = giver.TryIssueJobPackage(pawn, jobParams);
                if (result.IsValid) { return result; }
            }

            if (!Rand.Chance(PlayChance)) { return ThinkResult.NoJob; }
            return base.TryIssueJobPackage(pawn, jobParams);
        }

        // 지금 잡이 그 잡 기버가 준 놀이면 그 잡
        public static Job CurrentPlay(Pawn pawn, ThinkNode giver)
        {
            var job = pawn.CurJob;
            return job != null && job.jobGiver == giver ? job : null;
        }

        public static int Elapsed(Job job) => Find.TickManager.TicksGame - job.startTick;

        // 놀이 잡은 RethinkTicks마다 다시 생각하게 한다(일이 생기면 넘어가고, 아니면 이 묶음이 하던 놀이를 다시 준다).
        public static void SetRethink(Job job)
        {
            job.expiryInterval = RethinkTicks;
            job.checkOverrideOnExpire = true;
        }

        // 바깥 놀이를 해도 되는지: 바깥 위험(독성 낙진·유독 연무 등)이 없고, 그 칸 기온이 그 비비의 편한 기온 안이며, 저체온·열사병 기미가 없다.
        // 장기 계측(한랭 60일): 바깥 -30~-40°C에서 편자 놀이를 하던 아이가 저체온으로 쓰러져 죽었다(추정). 피신은 저체온이 가벼운 단계부터라 그 전에 막는다.
        public static bool OutdoorPlayOk(Pawn pawn, IntVec3 cell)
        {
            return TryGetOutdoorPlayRange(pawn, out var comfortable) && comfortable.Includes(GenTemperature.GetTemperatureForCell(cell, pawn.Map));
        }

        // 바깥 놀이 칸을 찾기 전에 보는 조건: OutdoorPlayOk에서 칸 기온을 바깥 기온으로 바꾼 것(지붕 없는 바깥 칸은 바깥 기온을 따른다).
        // 예전에는 빈둥거리기가 둥지 중심(방 안) 기온으로 미리 보고 한파에도 둘레 바깥 칸 289칸을 모두 검사해 1회 최대 0.4 ms였다(실측).
        // 참이면 그 비비의 편한 기온 범위를 넘겨 준다: 칸을 고르는 동안 바깥 위험·몸 상태는 바뀌지 않으므로 칸마다 OutdoorPlayOk 대신
        // 이 범위로 칸 기온만 보면 같은 판정이다(편한 기온은 옷·유전자 스탯이라 칸마다 다시 재지 않는다).
        public static bool OutdoorPlayWeatherOk(Pawn pawn, out FloatRange comfortable)
        {
            return TryGetOutdoorPlayRange(pawn, out comfortable) && comfortable.Includes(pawn.Map.mapTemperature.OutdoorTemp);
        }

        // 바깥 놀이의 기온 외 조건(바깥 위험·몸 상태)을 보고, 통과하면 그 비비의 편한 기온 범위를 넘겨 준다.
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
