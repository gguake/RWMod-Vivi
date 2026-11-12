using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestRefuel : ThinkNode_JobGiver
    {
        public static bool IsStarvedSeedling(Thing thing) => thing is ArcanePlant_Seedling seedling && !seedling.ManaComp.Active;

        public static bool IsIdleCooler(LordJob_ViviNest nest, Thing thing)
        {
            return ViviNestPlanner.IsCooler(thing) && !nest.Planner.NeedsCooling(thing.GetRoom());
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }

            var refuelables = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable);
            for (int i = 0; i < refuelables.Count; ++i)
            {
                var thing = refuelables[i];
                if (thing.Faction != nest.Faction || !WantsAutoRefuel(thing, out var comp) || IsStarvedSeedling(thing) || IsIdleCooler(nest, thing)) { continue; }
                if (!nest.Stock.AnyAllowedBy(comp.Props.fuelFilter)) { continue; }
                if (!pawn.CanReach(thing, PathEndMode.Touch, Danger.Some) || !RefuelWorkGiverUtility.CanRefuel(pawn, thing)) { continue; }

                return RefuelWorkGiverUtility.RefuelJob(pawn, thing);
            }

            return null;
        }

        private static bool WantsAutoRefuel(Thing thing, out CompRefuelable comp)
        {
            comp = thing.TryGetComp<CompRefuelable>();
            if (comp == null || thing.Fogged() || comp.IsFull || !comp.allowAutoRefuel) { return false; }
            if (comp.FuelPercentOfMax > 0f && !comp.Props.allowRefuelIfNotEmpty) { return false; }
            return comp.ShouldAutoRefuelNow;
        }
    }
}
