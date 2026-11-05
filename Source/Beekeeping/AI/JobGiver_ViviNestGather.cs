using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 비비가 둥지 채집 범위(ViviNestProduction.GatherRadius)의 꽃·나무로 가서 그 자리에서 꿀·꽃가루·프로폴리스를 채취한다(채집 장소·지시서 없음).
    // 무엇을 채집할지는 보유량 균형으로 가장 모자란 자원부터 고르고(그 자원을 줄 식물이 없으면 다음 자원), 어느 식물에서 할지는 기존 채집 레시피의 규칙
    // (채집 대상 식물, 자람·채집량·재채집 대기, 길 위험도, 기술 요구)으로 거른 것 중 가장 가까운 것이다. 밤과 바깥이 위험할 때는 쉰다.
    public class JobGiver_ViviNestGather : ThinkNode_JobGiver
    {
        private static List<(RecipeDef_Gathering recipe, JobDef job)> _gatherings;
        private static readonly List<(RecipeDef_Gathering recipe, JobDef job)> _tmpOrder = new List<(RecipeDef_Gathering recipe, JobDef job)>();
        private static readonly List<Thing> _tmpGatherables = new List<Thing>();
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

        // 채집 자원 중 가장 모자란 것의 모자란 정도
        public override float GetPriority(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return 0f; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return 0f; }

            return nest.Production.GatherPriority();
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (JobGiver_ViviNestRest.IsNight(pawn) || BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }

            var production = nest.Production;
            _tmpOrder.Clear();
            foreach (var gathering in Gatherings)
            {
                if (production.Need(gathering.recipe) > 0f && CanDo(pawn, gathering.recipe)) { _tmpOrder.Add(gathering); }
            }
            if (_tmpOrder.Count == 0) { return null; }

            // 삽입 정렬이라 모자란 정도가 같으면 꿀·꽃가루·프로폴리스 순서다.
            _tmpOrder.InsertionSort((a, b) => production.Need(b.recipe).CompareTo(production.Need(a.recipe)));

            CollectGatherables(pawn.Map, production);
            Job job = null;
            foreach (var gathering in _tmpOrder)
            {
                var target = FindTarget(pawn, gathering.recipe);
                if (target != null)
                {
                    job = JobMaker.MakeJob(gathering.job, target);
                    break;
                }
            }

            _tmpGatherables.Clear();
            _tmpCandidates.Clear();
            return job;
        }

        // 기존 채집 워크기버와 같은 조건: 레시피의 작업 속도·채집량 스탯이 0보다 크고(GatherWorker.PawnCanDoBill) 기술 요구(프로폴리스는 재배 8)를 채운다.
        // 연구는 플레이어 UI에서만 막으므로 보지 않는다(둥지 작업 지시서와 같다).
        private static bool CanDo(Pawn pawn, RecipeDef_Gathering recipe)
        {
            if (recipe.workSpeedStat != null && pawn.GetStatValue(recipe.workSpeedStat) <= 0f) { return false; }
            if (recipe.efficiencyStat != null && pawn.GetStatValue(recipe.efficiencyStat) <= 0f) { return false; }
            return recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn) == null;
        }

        // 채집 범위 안의 되풀이 채집 대상(기존 채집 시스템이 재채집 대기를 다는 것, CompRepeatGatherable)
        private static void CollectGatherables(Map map, ViviNestProduction production)
        {
            _tmpGatherables.Clear();
            var center = production.GatherCenter;
            var cellCount = GenRadial.NumCellsInRadius(ViviNestProduction.GatherRadius);
            for (int i = 0; i < cellCount; ++i)
            {
                var cell = center + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) { continue; }

                var things = map.thingGrid.ThingsListAtFast(cell);
                for (int j = 0; j < things.Count; ++j)
                {
                    var thing = things[j];
                    if (thing.Position == cell && thing is ThingWithComps withComps && withComps.GetComp<CompRepeatGatherable>() != null)
                    {
                        _tmpGatherables.Add(thing);
                    }
                }
            }
        }

        // 기존 채집 대상 판정(GatherWorker_Plant.FilterGatherableTarget, WorkGiver_GatheringBill)으로 거른 것 중 이 비비에게서 가장 가까운 것:
        // 레시피의 채집 대상(채집량 스탯이 있는 식물, 꽃가루는 마법식물 제외), 채집할 만큼 자람·채집량·재채집 대기, 병들거나 타지 않음,
        // 금지되지 않고 예약·도달할 수 있음(레시피 길 위험도), 대상이 따로 막지 않음(영원꽃 등).
        private static Thing FindTarget(Pawn pawn, RecipeDef_Gathering recipe)
        {
            _tmpCandidates.Clear();
            for (int i = 0; i < _tmpGatherables.Count; ++i)
            {
                var thing = _tmpGatherables[i];
                if (!recipe.fixedIngredientFilter.Allows(thing)) { continue; }
                if (!thing.CanGatherable(recipe.targetYieldStat, recipe.targetCooldownStat)) { continue; }
                if ((thing is Plant plant && plant.Blighted) || thing.IsBurning()) { continue; }

                _tmpCandidates.Add(thing);
            }
            if (_tmpCandidates.Count == 0) { return null; }

            // 가까운 것부터 보면 처음 닿는 것 뒤로는 길을 묻지 않는다.
            var position = pawn.Position;
            _tmpCandidates.SortBy(t => t.Position.DistanceToSquared(position));
            return GenClosest.ClosestThing_Global_Reachable(position, pawn.Map, _tmpCandidates, PathEndMode.Touch, TraverseParms.For(pawn, recipe.maxPathDanger),
                validator: t => !t.IsForbidden(pawn) && pawn.CanReserve(t) && !(t is IGatherableTarget target && !target.CanGatherByPawn(pawn, recipe)));
        }
    }
}
