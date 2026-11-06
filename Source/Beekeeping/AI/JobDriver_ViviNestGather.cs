using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 비비가 꽃·나무 곁에서 기존 채집 레시피 한 번을 채집 장소 없이 그 자리에서 해낸다.
    // 작업 시간·기술·경험치·채집 알림(재채집 대기, 서툰 채집의 식물 손상, 미지의 씨앗, 꽃향, 꽃가루 묻히기)은 기존 채집 잡(JobDriver_Gather)과 같고,
    // 채집 장소에 가서 하던 가공 몫도 그 자리에서 이어서 한다. 채취물은 모두 그 식물 둘레 바닥에 둥지 몫으로 떨어뜨리고, 옮기는 일은 둥지 운반 잡이 따로 한다.
    public class JobDriver_ViviNestGather : JobDriver
    {
        private const TargetIndex PlantInd = TargetIndex.A;

        private static readonly List<Thing> _tmpProducts = new List<Thing>();
        private static readonly List<Filth_Pollen> _tmpPollens = new List<Filth_Pollen>();

        private Thing Target => job.GetTarget(PlantInd).Thing;
        private RecipeDef_Gathering Recipe => JobGiver_ViviNestGather.RecipeOf(job.def);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var recipe = Recipe;
            this.FailOnDespawnedNullOrForbidden(PlantInd);
            this.FailOnBurningImmobile(PlantInd);
            this.FailOn(() => !Target.CanGatherable(recipe.targetYieldStat, recipe.targetCooldownStat));

            yield return Toils_Goto.GotoThing(PlantInd, PathEndMode.Touch);

            yield return Toils_General.Wait(WorkTicks(recipe), PlantInd)
                .FailOnCannotTouch(PlantInd, PathEndMode.Touch)
                .WithEffect(() => recipe.effectWorking, PlantInd)
                .PlaySustainerOrSound(() => recipe.soundWorking)
                .WithProgressBarToilDelay(PlantInd)
                .WithActiveSkill(() => recipe.workSkill);

            yield return Toils_General.Do(Gather);
        }

        // 기존 채집 잡과 같은 작업 시간: 채집 몫 ÷ 레시피 작업 속도 + 가공 몫 ÷ 작업 속도(채집 장소를 오가는 걸음만 없다)
        private int WorkTicks(RecipeDef_Gathering recipe)
        {
            var ticks = Mathf.CeilToInt(recipe.GatheringWorkAmount / (recipe.workSpeedStat != null ? pawn.GetStatValue(recipe.workSpeedStat) : 1f));
            if (recipe.ProcessingWorkAmount > 0)
            {
                ticks += Mathf.CeilToInt(recipe.ProcessingWorkAmount / pawn.GetStatValue(StatDefOf.WorkSpeedGlobal));
            }
            return ticks;
        }

        private void Gather()
        {
            var recipe = Recipe;
            var target = Target;
            var cell = target.Position;

            recipe.gatherWorker.Notify_Gathered(pawn, null, target, recipe);

            if (recipe.workSkill != null)
            {
                pawn.skills?.GetSkill(recipe.workSkill).Learn(recipe.workAmount * 0.1f * recipe.workSkillLearnFactor);
            }

            // 기존 채집 잡과 같은 채집량: 레시피 산출 × 채집량 스탯 × 대상 채집량 스탯 × 채집 장소 효율(둥지는 GatherYieldFactor)
            var efficiency = (recipe.efficiencyStat != null ? pawn.GetStatValue(recipe.efficiencyStat) : 1f) * ViviNestProduction.GatherYieldFactor;
            if (recipe.targetYieldStat != null)
            {
                efficiency *= target.GetStatValue(recipe.targetYieldStat);
            }

            var honey = recipe.gatherWorker is GatherWorker_Honey;
            _tmpProducts.Clear();
            foreach (var product in recipe.products)
            {
                var count = product.count * efficiency;
                if (honey)
                {
                    count = EatIfHoneyNeedy(product.thingDef, count);
                }
                BeekeepingUtility.MakeStacks(product.thingDef, GenMath.RoundRandom(count), _tmpProducts);
            }
            if (honey)
            {
                ShakeOffCarriedPollen();
            }

            for (int i = 0; i < _tmpProducts.Count; ++i)
            {
                BeekeepingUtility.PlaceProduct(pawn, _tmpProducts[i], cell);
            }
            _tmpProducts.Clear();
        }

        // 기존 꿀 채집(GatherWorker_Honey.Notify_RecipeComplete)처럼 꿀 의존이 심한(0.8 넘음) 비비는 거둔 꿀 하나를 그 자리에서 먹는다.
        private float EatIfHoneyNeedy(ThingDef productDef, float count)
        {
            var genes = pawn.genes?.GenesListForReading;
            if (genes == null) { return count; }

            for (int i = 0; i < genes.Count; ++i)
            {
                if (!(genes[i] is Gene_HoneyDependency gene)) { continue; }

                var hediff = gene.LinkedHediff;
                if (hediff != null && hediff.Severity > 0.8f && count > 1f)
                {
                    gene.Reset();
                    return Mathf.Clamp(count - 1f, 1f, productDef.stackLimit);
                }
                break;
            }
            return count;
        }

        // 기존 꿀 채집은 묻힌 꽃가루 오물을 채집 장소에서 꽃가루로 털어 냈다(GatherWorker_Honey.Notify_RecipeComplete → Filth_Pollen.GatherPollen:
        // 오물 정의의 양 × 청소 속도, 오물 하나당 한 겹). 채집 장소가 없으므로 그 자리에서 털어 낸다.
        private void ShakeOffCarriedPollen()
        {
            var filth = pawn.filth;
            if (filth == null) { return; }

            _tmpPollens.Clear();
            var carried = filth.CarriedFilthListForReading;
            for (int i = 0; i < carried.Count; ++i)
            {
                if (carried[i] is Filth_Pollen pollen) { _tmpPollens.Add(pollen); }
            }

            for (int i = 0; i < _tmpPollens.Count; ++i)
            {
                var pollen = _tmpPollens[i];
                var extension = pollen.def.GetModExtension<GatherableFilthExtension>();
                if (extension != null)
                {
                    var amount = Mathf.Max(1f, extension.amount * Mathf.Clamp(pawn.GetStatValue(extension.yieldStat), 0.1f, 2.0f));
                    BeekeepingUtility.MakeStacks(extension.thingDef, GenMath.RoundRandom(amount), _tmpProducts);
                }
                Pawn_FilthTracker_ReversePatch.ThinCarriedFilth(filth, pollen);
            }
            _tmpPollens.Clear();
        }
    }
}
