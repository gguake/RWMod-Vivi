using RimWorld;
using Verse;

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

        public Bill_ProductionWithFoodDrain()
        {
        }

        public Bill_ProductionWithFoodDrain(RecipeDef recipe, Precept_ThingStyle precept = null)
            : base(recipe, precept)
        {
        }

        public override bool PawnAllowedToStartAnew(Pawn p)
        {
            if (!base.PawnAllowedToStartAnew(p))
            {
                return false;
            }

            if (!p.IsVivi())
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

        public override void Notify_PawnDidWork(Pawn p)
        {
            if (p != null && p?.needs?.food != null)
            {
                var extension = RecipeModExtension;
                if (extension != null && extension.foodDrains > 0f)
                {
                    p.needs.food.CurLevel -= extension.foodDrains / recipe.workAmount;
                }
            }
        }
    }
}
