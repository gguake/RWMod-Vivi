using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 들고 온 짐(인벤토리)을 둥지 보관 칸에 하나씩 풀어 놓는다. 바닐라 짐 풀기는 플레이어 비축 구역으로도 옮기므로 쓰지 않는다.
    public class JobGiver_ViviNestUnload : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn.GetLord()?.LordJob is LordJob_ViviNest nest) || !nest.Settled) { return null; }

            var thing = FirstUnloadable(pawn);
            if (thing == null) { return null; }

            if (!nest.Stock.TryFindStorageCellFor(thing, pawn, ViviNestStorageRank.None, out var cell)) { return null; }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ViviNestUnload, cell, thing);
            job.count = thing.stackCount;
            return job;
        }

        public static Thing FirstUnloadable(Pawn pawn)
        {
            var inventory = pawn.inventory?.innerContainer;
            if (inventory == null) { return null; }

            for (int i = 0; i < inventory.Count; ++i)
            {
                var thing = inventory[i];
                if (thing.def.category == ThingCategory.Item && !(thing is Pawn))
                {
                    return thing;
                }
            }

            return null;
        }
    }
}
