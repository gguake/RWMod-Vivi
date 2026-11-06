using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 새싹·방출기(마나 비료)와 냉각기(원목) 등 연료를 먹는 둥지 것을 바닐라 연료 보충으로 채운다(연료는 둥지 몫만 쓴다).
    public class JobGiver_ViviNestRefuel : ThinkNode_JobGiver
    {
        // 마나가 없어 자라지 못하는 새싹(마나 비활성)은 채우지 않는다. 새싹은 마나와 상관없이 비료를 하루 10개씩 태우므로 채워 봐야 비료만 먹는다
        // (장기 계측: 마나 0 칸의 새싹 14그루가 28~60일 동안 자라지 못한 채 비료를 먹어 땅 파기 비중이 3%→11%로 늘었다).
        // 마나가 돌아오면 다시 채운다. 연료가 떨어진 뒤로도 4일 가까이 버티므로 방출기 연료가 잠깐 떨어진 동안에는 죽지 않는다.
        public static bool IsStarvedSeedling(Thing thing) => thing is ArcanePlant_Seedling seedling && !seedling.ManaComp.Active;

        // 서늘한 철의 냉각기는 채우지 않는다(ViviNestPlanner.NeedsCooling): 바닐라 냉각기는 원목을 늘 하루 10개씩 태우지만 17°C 아래로는 식히지 않고,
        // 잔불꽃이 데우는 방에서는 그 열을 깎는다. 남은 연료가 떨어지면 저절로 멈추고, 더운 철이 오면 다시 채운다.
        public static bool IsIdleCooler(LordJob_ViviNest nest, Thing thing)
        {
            return ViviNestPlanner.IsCooler(thing) && !nest.Planner.NeedsCooling(thing.GetRoom());
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }

            var refuelables = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable);
            for (int i = 0; i < refuelables.Count; ++i)
            {
                var thing = refuelables[i];
                if (thing.Faction != nest.Faction || !WantsAutoRefuel(thing) || IsStarvedSeedling(thing) || IsIdleCooler(nest, thing)) { continue; }
                if (!pawn.CanReach(thing, PathEndMode.Touch, Danger.Some) || !RefuelWorkGiverUtility.CanRefuel(pawn, thing)) { continue; }

                return RefuelWorkGiverUtility.RefuelJob(pawn, thing);
            }

            return null;
        }

        // 바닐라 연료 보충 판정(RefuelWorkGiverUtility.CanRefuel) 중 비비와 상관없는 앞 조건: 안개, 가득 참, 자동 보충, 비지 않았을 때 보충 금지, 자동 보충할 때.
        // 길(CanReach)보다 먼저 본다. 예전에는 연료가 찬 둥지 새싹·방출기마다 길부터 물어 연료 보충이 1회 16~22 µs였다(실측, 거의 늘 실패).
        private static bool WantsAutoRefuel(Thing thing)
        {
            var comp = thing.TryGetComp<CompRefuelable>();
            if (comp == null || thing.Fogged() || comp.IsFull || !comp.allowAutoRefuel) { return false; }
            if (comp.FuelPercentOfMax > 0f && !comp.Props.allowRefuelIfNotEmpty) { return false; }
            return comp.ShouldAutoRefuelNow;
        }
    }
}
