using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestUnload : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled) { return null; }

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
