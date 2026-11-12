using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ViviNestGather : ThinkNode_JobGiver
    {
        private static List<(RecipeDef_Gathering recipe, JobDef job)> _gatherings;
        private static readonly List<(RecipeDef_Gathering recipe, JobDef job)> _tmpOrder = new List<(RecipeDef_Gathering recipe, JobDef job)>();
        private static readonly List<Thing> _tmpGatherables = new List<Thing>();
        private static readonly List<CompRepeatGatherable> _tmpGatherableComps = new List<CompRepeatGatherable>();
        private static readonly List<Thing> _tmpCandidates = new List<Thing>();

        private static List<(RecipeDef_Gathering recipe, JobDef job)> Gatherings
        {
            get
            {
                if (_gatherings == null)
                {
                    _gatherings = new List<(RecipeDef_Gathering recipe, JobDef job)>
                    {
                        ((RecipeDef_Gathering)VVRecipeDefOf.VV_GatherHoney, VVJobDefOf.VV_ViviNestGatherHoney),
                        ((RecipeDef_Gathering)VVRecipeDefOf.VV_GatherPollen, VVJobDefOf.VV_ViviNestGatherPollen),
                        ((RecipeDef_Gathering)VVRecipeDefOf.VV_GatherPropolis, VVJobDefOf.VV_ViviNestGatherPropolis),
                    };
                }
                return _gatherings;
            }
        }

        public static RecipeDef_Gathering RecipeOf(JobDef job)
        {
            foreach (var gathering in Gatherings)
            {
                if (gathering.job == job) { return gathering.recipe; }
            }
            return null;
        }

        public override float GetPriority(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            if (nest == null) { return 0f; }

            var production = nest.Production;
            foreach (var gathering in Gatherings)
            {
                if (CanTry(pawn, gathering.recipe, production)) { return production.GatherPriority(); }
            }
            return 0f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = WorkingNest(pawn);
            if (nest == null) { return null; }

            var production = nest.Production;
            _tmpOrder.Clear();
            foreach (var gathering in Gatherings)
            {
                if (CanTry(pawn, gathering.recipe, production)) { _tmpOrder.Add(gathering); }
            }
            if (_tmpOrder.Count == 0) { return null; }

            _tmpOrder.InsertionSort((a, b) => production.Need(b.recipe).CompareTo(production.Need(a.recipe)));

            CollectGatherables(pawn.Map, nest.Territory);
            Job job = null;
            foreach (var gathering in _tmpOrder)
            {
                var target = FindTarget(pawn, gathering.recipe, out var anyCandidate, out var readyTick);
                if (target != null)
                {
                    job = JobMaker.MakeJob(gathering.job, target);
                    break;
                }

                if (!anyCandidate) { production.Notify_NoGatherTarget(gathering.recipe, readyTick); }
            }

            _tmpGatherables.Clear();
            _tmpGatherableComps.Clear();
            _tmpCandidates.Clear();
            return job;
        }

        private static LordJob_ViviNest WorkingNest(Pawn pawn) => BeekeepingUtility.OutdoorWorkingNest(pawn);

        private static bool CanTry(Pawn pawn, RecipeDef_Gathering recipe, ViviNestProduction production)
        {
            return production.Need(recipe) > 0f && !production.GatherWaiting(recipe) && CanDo(pawn, recipe, production);
        }

        private static bool CanDo(Pawn pawn, RecipeDef_Gathering recipe, ViviNestProduction production)
        {
            if (!production.TryGetGatherStats(pawn, recipe, out var statsOk))
            {
                statsOk = !(recipe.workSpeedStat != null && pawn.GetStatValue(recipe.workSpeedStat) <= 0f)
                    && !(recipe.efficiencyStat != null && pawn.GetStatValue(recipe.efficiencyStat) <= 0f);
                production.SetGatherStats(pawn, recipe, statsOk);
            }
            return statsOk && recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn) == null;
        }

        private static void CollectGatherables(Map map, ViviNestTerritory territory)
        {
            _tmpGatherables.Clear();
            _tmpGatherableComps.Clear();
            var cells = territory.ActivityCells;
            var cellCount = territory.ActivityCellCount(ViviNestProduction.GatherRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = cells[i];
                var things = map.thingGrid.ThingsListAtFast(cell);
                for (int j = 0; j < things.Count; ++j)
                {
                    var thing = things[j];
                    if (thing.Position != cell || !(thing is ThingWithComps withComps)) { continue; }

                    var comp = withComps.GetComp<CompRepeatGatherable>();
                    if (comp != null)
                    {
                        _tmpGatherables.Add(thing);
                        _tmpGatherableComps.Add(comp);
                    }
                }
            }
        }

        private static Thing FindTarget(Pawn pawn, RecipeDef_Gathering recipe, out bool anyCandidate, out int readyTick)
        {
            anyCandidate = false;
            readyTick = -1;
            var ticks = Find.TickManager.TicksGame;
            var filter = recipe.fixedIngredientFilter;
            var cooldownStat = recipe.targetCooldownStat;
            var yieldStat = recipe.targetYieldStat;
            _tmpCandidates.Clear();
            for (int i = 0; i < _tmpGatherables.Count; ++i)
            {
                var thing = _tmpGatherables[i];
                if (!filter.OnlySpecialFilters && !filter.Allows(thing.def)) { continue; }

                if (cooldownStat != null)
                {
                    var cooldownEnd = _tmpGatherableComps[i].CooldownEndTick(cooldownStat);
                    if (cooldownEnd > ticks)
                    {
                        readyTick = BeekeepingUtility.EarlierTick(readyTick, cooldownEnd);
                        continue;
                    }
                }

                var plant = thing as Plant;
                if (plant != null)
                {
                    var minGrowth = thing.GetStatValue(VVStatDefOf.VV_MinGrowthPlantGatherable);
                    if (plant.Growth < minGrowth)
                    {
                        readyTick = BeekeepingUtility.EarlierTick(readyTick, BeekeepingUtility.GrowthReadyTick(plant, minGrowth, ViviNestProduction.GatherRetryMaxTicks));
                        continue;
                    }
                }
                if (yieldStat != null && thing.GetStatValue(yieldStat, cacheStaleAfterTicks: 2000) <= 0f) { continue; }
                if (!filter.Allows(thing)) { continue; }
                if ((plant != null && plant.Blighted) || thing.IsBurning()) { continue; }

                _tmpCandidates.Add(thing);
            }
            if (_tmpCandidates.Count == 0) { return null; }

            anyCandidate = true;
            
            var position = pawn.Position;
            _tmpCandidates.SortBy(t => t.Position.DistanceToSquared(position));
            return GenClosest.ClosestThing_Global_Reachable(position, pawn.Map, _tmpCandidates, PathEndMode.Touch, TraverseParms.For(pawn, recipe.maxPathDanger),
                validator: t => !t.IsForbidden(pawn) && pawn.CanReserve(t) && !(t is IGatherableTarget target && !target.CanGatherByPawn(pawn, recipe)));
        }
    }
}
