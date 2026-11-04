using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 둥지 물건을 더 좋은 보관 칸(선반 > 방 바닥 > 바깥)으로 옮기고, 둥지 몫으로 표시한 바깥 물건(수확물 등)을 들여온다.
    public class JobGiver_ViviNestHaul : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn.GetLord()?.LordJob is LordJob_ViviNest nest) || !nest.Settled) { return null; }
            if (pawn.DevelopmentalStage.Baby()) { return null; }

            var stock = nest.Stock;
            foreach (var thing in stock.HaulCandidates())
            {
                if (thing.IsBurning() || !pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Some)) { continue; }

                var rank = stock.RankOf(thing.Position);
                if (!stock.TryFindStorageCellFor(thing, pawn, rank, out var cell)) { continue; }

                var job = JobMaker.MakeJob(JobDefOf.HaulToCell, thing, cell);
                job.count = thing.stackCount;
                job.haulMode = stock.RankOf(cell) == ViviNestStorageRank.Shelf ? HaulMode.ToCellStorage : HaulMode.ToCellNonStorage;
                return job;
            }

            return null;
        }
    }
}
