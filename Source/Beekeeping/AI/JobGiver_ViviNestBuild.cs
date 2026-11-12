using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestBuild : ThinkNode_JobGiver
    {
        private static readonly List<Thing> _candidates = new List<Thing>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) { return null; }
            if (pawn.workSettings != null && !pawn.workSettings.EverWork) { EnsureBuilderWorkSettings(pawn); }

            return ConstructionJob(pawn, nest) ?? RepairJob(pawn, nest) ?? RoofJob(pawn, nest);
        }

        private static Job ConstructionJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var finish = VVWorkGiverDefOf.ConstructFinishFrames.Worker as WorkGiver_Scanner;
            var deliverFrames = VVWorkGiverDefOf.ConstructDeliverResourcesToFrames.Worker as WorkGiver_Scanner;
            var deliverBlueprints = VVWorkGiverDefOf.ConstructDeliverResourcesToBlueprints.Worker as WorkGiver_Scanner;

            var position = pawn.Position;
            _candidates.Clear();
            foreach (var thing in nest.Planner.CachedNestConstructions())
            {
                if (thing.Spawned) { _candidates.Add(thing); }
            }
            _candidates.SortBy(t => t.Position.DistanceToSquared(position));

            var stock = nest.Stock;
            foreach (var thing in _candidates)
            {
                Job job;
                if (thing is Frame)
                {
                    job = finish.JobOnThing(pawn, thing);
                    if (job == null && !LacksMaterials(thing, stock, pawn)) { job = deliverFrames.JobOnThing(pawn, thing); }
                }
                else
                {
                    job = LacksMaterials(thing, stock, pawn) ? null : deliverBlueprints.JobOnThing(pawn, thing);
                }

                if (job != null) { return job; }
            }
            return null;
        }

        private static bool LacksMaterials(Thing thing, ViviNestStock stock, Pawn pawn)
        {
            if (thing is Blueprint_Install || !(thing is IConstructible constructible)) { return false; }
            if (thing is Blueprint && thing.def.entityDefToBuild is TerrainDef && thing.Map.terrainGrid.CanRemoveTopLayerAt(thing.Position)) { return false; }

            var needed = false;
            var costs = constructible.TotalMaterialCost();
            for (int i = 0; i < costs.Count; ++i)
            {
                var def = costs[i].thingDef;
                if (constructible.ThingCountNeeded(def) <= 0) { continue; }
                if (stock.CountOf(def) > 0) { return false; }
                needed = true;
            }

            return needed && GenConstruct.FirstBlockingThing(thing, pawn) == null;
        }

        private static Job RepairJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var repair = VVWorkGiverDefOf.Repair.Worker as WorkGiver_Scanner;
            var repairable = pawn.Map.listerBuildingsRepairable.RepairableBuildings(nest.Faction);
            for (int i = 0; i < repairable.Count; ++i)
            {
                var thing = repairable[i];
                if (repair.HasJobOnThing(pawn, thing) && pawn.CanReach(thing, repair.PathEndMode, repair.MaxPathDanger(pawn)))
                {
                    return repair.JobOnThing(pawn, thing);
                }
            }
            return null;
        }

        private static Job RoofJob(Pawn pawn, LordJob_ViviNest nest)
        {
            var map = pawn.Map;
            foreach (var cell in nest.Planner.CellsNeedingRoof())
            {
                if (!ViviNestPlanner.CanRoofNow(cell, map) || !RoofCollapseUtility.ConnectedToRoofHolder(cell, map, assumeRoofAtRoot: true)) { continue; }
                if (!pawn.CanReserve(cell, 1, -1, ReservationLayerDefOf.Ceiling) || !pawn.CanReach(cell, PathEndMode.Touch, Danger.Some)) { continue; }

                return JobMaker.MakeJob(VVJobDefOf.VV_ViviNestBuildRoof, cell, cell);
            }
            return null;
        }

        public static void EnsureBuilderWorkSettings(Pawn pawn)
        {
            if (pawn.workSettings == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)) { return; }

            if (!pawn.workSettings.EverWork)
            {
                pawn.workSettings.EnableAndInitialize();
            }
            pawn.workSettings.SetPriority(WorkTypeDefOf.Construction, 1);
        }
    }
}
