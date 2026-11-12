using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestHaul : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }

            var stock = nest.Stock;
            
            stock.BeginHaulScan();
            foreach (var thing in stock.HaulCandidates())
            {
                if (thing is UnfinishedThing || thing.IsBurning() || !pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Some)) { continue; }

                var rank = stock.RankOf(thing.Position);
                if (!stock.TryFindHaulStorageCell(thing, pawn, rank, out var cell)) { continue; }

                var job = JobMaker.MakeJob(JobDefOf.HaulToCell, thing, cell);
                job.count = stock.StorableCount(cell, thing);
                job.haulMode = ViviNestStock.IsShelfRank(stock.RankOf(cell)) ? HaulMode.ToCellStorage : HaulMode.ToCellNonStorage;
                return job;
            }

            return null;
        }
    }
}
