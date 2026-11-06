using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 들고 온 짐(인벤토리)을 둥지 보관 칸에 하나씩 풀어 놓는다. 바닐라 짐 풀기는 플레이어 비축 구역으로도 옮기므로 쓰지 않는다.
    public class JobGiver_ViviNestUnload : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled) { return null; }

            // 지닌 것 중 첫 물건(폰 제외)
            var inventory = pawn.inventory?.innerContainer;
            if (inventory == null) { return null; }

            Thing thing = null;
            for (int i = 0; i < inventory.Count; ++i)
            {
                if (inventory[i].def.category == ThingCategory.Item && !(inventory[i] is Pawn))
                {
                    thing = inventory[i];
                    break;
                }
            }
            if (thing == null) { return null; }

            if (!nest.Stock.TryFindStorageCellFor(thing, pawn, ViviNestStorageRank.None, out var cell)) { return null; }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ViviNestUnload, cell, thing);
            job.count = nest.Stock.StorableCount(cell, thing);
            return job;
        }
    }
}
