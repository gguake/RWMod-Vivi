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
                // 선반의 보관 설정을 따르지 않으므로 선반에도 비보관 방식으로 놓는다(보관 방식은 이동 중 보관 설정을 다시 검사해 실패한다).
                job.haulMode = HaulMode.ToCellNonStorage;
                return job;
            }

            return null;
        }
    }
}
