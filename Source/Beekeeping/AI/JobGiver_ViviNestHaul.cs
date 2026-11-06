using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 몫으로 표시한 바깥 물건(채취물·수확물 등)과 방 바닥 물건을 더 좋은 보관 칸(둥지 방 밖 정착 범위의 선반 > 방 안 선반 > 방 바닥)으로 옮긴다.
    // 옮길 곳이 없으면 그 자리에 둔다. 미완성품은 만들던 비비가 그 작업 자리에서 이어 만들므로 옮기지 않는다.
    public class JobGiver_ViviNestHaul : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled) { return null; }
            if (pawn.DevelopmentalStage.Baby()) { return null; }

            var stock = nest.Stock;
            foreach (var thing in stock.HaulCandidates())
            {
                if (thing is UnfinishedThing || thing.IsBurning() || !pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Some)) { continue; }

                var rank = stock.RankOf(thing.Position);
                if (!stock.TryFindStorageCellFor(thing, pawn, rank, out var cell)) { continue; }

                var job = JobMaker.MakeJob(JobDefOf.HaulToCell, thing, cell);
                job.count = stock.StorableCount(cell, thing);
                job.haulMode = ViviNestStock.IsShelfRank(stock.RankOf(cell)) ? HaulMode.ToCellStorage : HaulMode.ToCellNonStorage;
                return job;
            }

            return null;
        }
    }
}
