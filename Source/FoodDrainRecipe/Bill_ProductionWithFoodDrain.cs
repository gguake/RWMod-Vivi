using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class Bill_ProductionWithFoodDrain : Bill_Production
    {
        private RecipeModExtension _extension;
        public RecipeModExtension RecipeModExtension
        {
            get
            {
                if (_extension == null)
                {
                    _extension = recipe.GetModExtension<RecipeModExtension>();
                }
                return _extension;
            }
        }

        // 마지막으로 식량을 차감한 시점의 작업 틱 누계 (JobDriver_DoBill.ticksSpentDoingRecipeWork). -1이면 기준이 없다.
        private int _lastWorkTicks = -1;

        public Bill_ProductionWithFoodDrain()
        {
        }

        public Bill_ProductionWithFoodDrain(RecipeDef recipe, Precept_ThingStyle precept = null)
            : base(recipe, precept)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _lastWorkTicks, "lastWorkTicks", -1);
        }

        public override bool PawnAllowedToStartAnew(Pawn p)
        {
            if (!base.PawnAllowedToStartAnew(p))
            {
                return false;
            }

            var extension = RecipeModExtension;
            if (extension != null)
            {
                var needFood = p.needs?.food;
                if (needFood != null && needFood.CurLevel < extension.foodDrains)
                {
                    return false;
                }
            }

            return true;
        }

        public override void Notify_BillWorkStarted(Pawn billDoer)
        {
            base.Notify_BillWorkStarted(billDoer);

            _lastWorkTicks = 0;
        }

        // 폰의 작업 틱은 카메라에 따라 1~15틱 간격으로 묶여 불리므로, 호출 횟수가 아니라 그 사이에 처리한 작업량만큼 식량을 줄인다.
        public override void Notify_PawnDidWork(Pawn p)
        {
            if (!(p?.jobs?.curDriver is JobDriver_DoBill driver)) { return; }

            var spentTicks = driver.ticksSpentDoingRecipeWork;
            // 기준이 없으면(이 값을 저장하기 전의 세이브) 한 틱만 계산한다.
            var delta = _lastWorkTicks < 0 || spentTicks < _lastWorkTicks ? 1 : spentTicks - _lastWorkTicks;
            _lastWorkTicks = spentTicks;

            var needFood = p.needs?.food;
            var extension = RecipeModExtension;
            if (delta <= 0 || needFood == null || extension == null || extension.foodDrains <= 0f) { return; }

            var totalWork = GetWorkAmount(driver.job.GetTarget(TargetIndex.B).Thing);
            if (totalWork <= 0f) { return; }

            // 바닐라가 이번 간격에 줄이는 작업량과 같은 식 (Toils_Recipe.DoRecipeWork)
            var workSpeed = recipe.workSpeedStat != null ? p.GetStatValue(recipe.workSpeedStat) : 1f;
            if (recipe.workTableSpeedStat != null && driver.BillGiver is Building_WorkTable workTable)
            {
                workSpeed *= workTable.GetStatValue(recipe.workTableSpeedStat);
            }
            if (DebugSettings.fastCrafting)
            {
                workSpeed *= 30f;
            }

            var workDone = Mathf.Min(Mathf.Max(0f, driver.workLeft), Mathf.Max(0f, workSpeed * delta));
            needFood.CurLevel -= extension.foodDrains * workDone / totalWork;
        }
    }
}
