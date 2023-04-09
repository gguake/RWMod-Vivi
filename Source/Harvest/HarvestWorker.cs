using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public abstract class HarvestWorker
    {
        public abstract string JobFailReasonIfNoHarvestable { get; }

        public abstract bool CanDoBill(Pawn pawn, Bill bill);

        public abstract IEnumerable<Thing> FindAllHarvestTargetInRegion(Pawn pawn, Region region, Thing billGiver, Bill bill);

        public abstract bool TryMakeJob(Pawn pawn, Thing billGiver, IEnumerable<Thing> targets, Bill bill, out Job job);
    }
}
