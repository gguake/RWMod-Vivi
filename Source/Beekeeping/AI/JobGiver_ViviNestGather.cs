using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 비비가 둥지 채집 범위(ViviNestProduction.GatherRadius)의 꽃·나무로 가서 그 자리에서 꿀·꽃가루·프로폴리스를 채취한다(채집 장소·지시서 없음).
    // 무엇을 채집할지는 보유량 균형으로 가장 모자란 자원부터 고르고(그 자원을 줄 식물이 없으면 다음 자원), 어느 식물에서 할지는 기존 채집 레시피의 규칙
    // (채집 대상 식물, 자람·채집량·재채집 대기, 길 위험도, 기술 요구)으로 거른 것 중 가장 가까운 것이다. 밤과 바깥이 위험할 때는 쉰다.
    // 채집할 대상이 하나도 없는 자원은 대상이 생길 수 있는 가장 이른 틱(재채집 대기 끝, 자람, 상한 GatherRetryMaxTicks)까지 둥지가 기다린다(ViviNestProduction).
    public class JobGiver_ViviNestGather : ThinkNode_JobGiver
    {
        private static List<(RecipeDef_Gathering recipe, JobDef job)> _gatherings;
        private static readonly List<(RecipeDef_Gathering recipe, JobDef job)> _tmpOrder = new List<(RecipeDef_Gathering recipe, JobDef job)>();
        private static readonly List<Thing> _tmpGatherables = new List<Thing>();
        private static readonly List<CompRepeatGatherable> _tmpGatherableComps = new List<CompRepeatGatherable>();
        private static readonly List<Thing> _tmpCandidates = new List<Thing>();

        // 둥지가 채집하는 기존 채집 레시피와 그것을 그 자리에서 해내는 둥지 채집 잡
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

        // 둥지 채집 잡이 해내는 채집 레시피
        public static RecipeDef_Gathering RecipeOf(JobDef job)
        {
            foreach (var gathering in Gatherings)
            {
                if (gathering.job == job) { return gathering.recipe; }
            }
            return null;
        }

        // 채집 자원 중 가장 모자란 것의 모자란 정도. 이 비비가 할 수 있으면서 대상을 기다리는 중이 아닌 모자란 채집이 없으면 0이다
        // (바닐라 우선도 정렬은 우선도가 0보다 크면 실패해도 사고마다 TryGiveJob을 부른다).
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

            // 삽입 정렬이라 모자란 정도가 같으면 꿀·꽃가루·프로폴리스 순서다.
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

                // 채집할 대상이 아예 없을 때만 둥지 전체가 그 자원을 기다린다(대상이 있는데 이 비비가 예약·도달하지 못한 것이면 다른 구성원은 할 수 있다).
                if (!anyCandidate) { production.Notify_NoGatherTarget(gathering.recipe, readyTick); }
            }

            _tmpGatherables.Clear();
            _tmpGatherableComps.Clear();
            _tmpCandidates.Clear();
            return job;
        }

        // 채집할 수 있는 둥지(GetPriority·TryGiveJob 공통 조건): 정착했고, 아기가 아니며, 바깥 일을 쉬지 않을 때
        private static LordJob_ViviNest WorkingNest(Pawn pawn) => BeekeepingUtility.OutdoorWorkingNest(pawn);

        // 모자라고, 대상을 기다리는 중이 아니며, 이 비비가 할 수 있는 채집
        private static bool CanTry(Pawn pawn, RecipeDef_Gathering recipe, ViviNestProduction production)
        {
            return production.Need(recipe) > 0f && !production.GatherWaiting(recipe) && CanDo(pawn, recipe, production);
        }

        // 기존 채집 워크기버와 같은 조건: 레시피의 작업 속도·채집량 스탯이 0보다 크고(GatherWorker.PawnCanDoBill) 기술 요구(프로폴리스는 재배 8)를 채운다.
        // 연구는 플레이어 UI에서만 막으므로 보지 않는다(둥지 작업 지시서와 같다). 스탯 판정은 둥지가 구성원마다 잠깐 기억한다(GatherStatsCacheTicks).
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

        // 채집 범위 안의 되풀이 채집 대상(기존 채집 시스템이 재채집 대기를 다는 것, CompRepeatGatherable)과 그 부품
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

        // 기존 채집 대상 판정(GatherWorker_Plant.FilterGatherableTarget, GatherUtility.CanGatherable, WorkGiver_GatheringBill)으로 거른 것 중 이 비비에게서 가장 가까운 것:
        // 레시피의 채집 대상(채집량 스탯이 있는 식물, 꽃가루는 마법식물 제외), 채집할 만큼 자람·채집량·재채집 대기, 병들거나 타지 않음,
        // 금지되지 않고 예약·도달할 수 있음(레시피 길 위험도), 대상이 따로 막지 않음(영원꽃 등).
        // 판정은 싼 것부터 한다: 정의 필터 → 재채집 대기 → 자람 → 채집량 → 전체 필터(내구도 등) → 병·불. 꽃 대부분은 재채집 대기라 자람·채집량 스탯을 재지 않고 넘어간다.
        // anyCandidate는 비비와 상관없이 채집할 수 있는 대상이 있었는지, readyTick은 대기·자람으로 빠진 대상 중 가장 이른 준비 틱(모르면 -1)이다.
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
                // 전체 필터(Allows(Thing))도 정의를 먼저 본다(특수 필터만 쓰는 필터 제외).
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
            // 가까운 것부터 보면 처음 닿는 것 뒤로는 길을 묻지 않는다.
            var position = pawn.Position;
            _tmpCandidates.SortBy(t => t.Position.DistanceToSquared(position));
            return GenClosest.ClosestThing_Global_Reachable(position, pawn.Map, _tmpCandidates, PathEndMode.Touch, TraverseParms.For(pawn, recipe.maxPathDanger),
                validator: t => !t.IsForbidden(pawn) && pawn.CanReserve(t) && !(t is IGatherableTarget target && !target.CanGatherByPawn(pawn, recipe)));
        }
    }
}
