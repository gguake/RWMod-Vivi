using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestPlantSeed : ThinkNode_JobGiver
    {
        public const float OutdoorSearchRadius = 12f;
        
        private const float MinSeedlingMana = 5f;
        public const float MinFertility = 0.7f;

        private static List<ThingDef> _seedDefs;

        private static List<ThingDef> SeedDefs
        {
            get
            {
                if (_seedDefs == null)
                {
                    _seedDefs = new List<ThingDef>();
                    foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                    {
                        if (def.HasComp(typeof(CompArcaneSeed))) { _seedDefs.Add(def); }
                    }
                }
                return _seedDefs;
            }
        }

        public static bool IsPotPlant(ThingDef plantDef)
        {
            return plantDef != null && plantDef == VVThingDefOf.VV_EmberBloom;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn)) { return null; }

            var map = pawn.Map;
            foreach (var seedDef in SeedDefs)
            {
                var seeds = map.listerThings.ThingsOfDef(seedDef);
                for (int i = 0; i < seeds.Count; ++i)
                {
                    var seed = seeds[i];
                    var comp = seed.TryGetComp<CompArcaneSeed>();
                    if (comp == null || !nest.Stock.IsNestOwned(seed)) { continue; }
                    if (!pawn.CanReserveAndReach(seed, PathEndMode.Touch, Danger.Some)) { continue; }

                    var potPlant = IsPotPlant(comp.Props.targetPlantDef);
                    if (!potPlant && BeekeepingUtility.OutdoorDangerous(map)) { continue; }

                    var cell = potPlant ? FindPotCell(nest, comp, pawn) : FindOutdoorCell(nest, comp, pawn);
                    if (!cell.IsValid) { continue; }

                    var plant = cell.GetPlant(map);
                    if (plant != null)
                    {
                        if (!pawn.CanReserve(plant) || !PlantUtility.PawnWillingToCutPlant_Job(plant, pawn)) { continue; }
                        return JobMaker.MakeJob(JobDefOf.CutPlant, plant);
                    }

                    comp.SeedlingCells.Clear();
                    comp.SeedlingCells.Add(cell);
                    var job = JobMaker.MakeJob(VVJobDefOf.VV_PlantArcaneSeed, seed, cell);
                    job.count = 1;
                    return job;
                }
            }

            return null;
        }

        private static IntVec3 FindPotCell(LordJob_ViviNest nest, CompArcaneSeed comp, Pawn pawn)
        {
            var map = pawn.Map;
            return nest.Planner.FindEmberPotCell(cell => comp.CanSowAt(cell, map).Accepted && pawn.CanReserveAndReach(cell, PathEndMode.Touch, Danger.Some));
        }

        private const float SpreadRadius = 7f;

        private static readonly List<IntVec3> _tmpPlantCells = new List<IntVec3>();

        private static IntVec3 FindOutdoorCell(LordJob_ViviNest nest, CompArcaneSeed comp, Pawn pawn)
        {
            var map = pawn.Map;
            var mana = map.GetManaComponent();
            if (mana == null) { return IntVec3.Invalid; }

            var territory = nest.Territory;
            _tmpPlantCells.Clear();
            var buildings = map.listerBuildings.allBuildingsNonColonist;
            for (int i = 0; i < buildings.Count; ++i)
            {
                var building = buildings[i];
                if (building is ArcanePlant && building.Faction == nest.Faction && territory.InActivityRange(building.Position, OutdoorSearchRadius + SpreadRadius))
                {
                    _tmpPlantCells.Add(building.Position);
                }
            }

            var best = IntVec3.Invalid;
            var bestCrowd = int.MaxValue;
            var bestFertility = 0f;
            var bestMana = 0f;
            var cells = territory.ActivityCells;
            var cellCount = territory.ActivityCellCount(OutdoorSearchRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = cells[i];
                var cellMana = mana[cell];
                if (cellMana < MinSeedlingMana) { continue; }
                var fertility = map.fertilityGrid.FertilityAt(cell);
                if (fertility < MinFertility) { continue; }

                var crowd = 0;
                for (int j = 0; j < _tmpPlantCells.Count; ++j)
                {
                    if (_tmpPlantCells[j].InHorDistOf(cell, SpreadRadius)) { crowd++; }
                }
                if (crowd > bestCrowd || (crowd == bestCrowd && (fertility < bestFertility || (fertility == bestFertility && cellMana <= bestMana)))) { continue; }
                if (!nest.Planner.IsFreeOutdoorCell(cell, allowPlants: true)) { continue; }
                if (!comp.CanSowAt(cell, map).Accepted || !pawn.CanReach(cell, PathEndMode.Touch, Danger.Some)) { continue; }

                best = cell;
                bestCrowd = crowd;
                bestFertility = fertility;
                bestMana = cellMana;
            }
            _tmpPlantCells.Clear();
            return best;
        }
    }
}
