using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
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

        private int WorkTicks(RecipeDef_Gathering recipe)
        {
            var ticks = recipe.GatheringTicks(pawn);
            if (recipe.ProcessingWorkAmount > 0)
            {
                ticks += recipe.ProcessingTicks(pawn);
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
                    count = GatherWorker_Honey.EatIfHoneyNeedy(pawn, product.thingDef, count);
                    ShakeOffCarriedPollen();
                }
                BeekeepingUtility.MakeStacks(product.thingDef, GatherUtility.RollCount(count), _tmpProducts);
            }

            for (int i = 0; i < _tmpProducts.Count; ++i)
            {
                BeekeepingUtility.PlaceProduct(pawn, _tmpProducts[i], cell);
            }
            _tmpProducts.Clear();
        }

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
                    BeekeepingUtility.MakeStacks(extension.thingDef, GatherUtility.RollCount(extension.YieldFor(pawn)), _tmpProducts);
                }
                Pawn_FilthTracker_ReversePatch.ThinCarriedFilth(filth, pollen);
            }
            _tmpPollens.Clear();
        }
    }
}
