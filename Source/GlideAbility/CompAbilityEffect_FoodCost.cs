using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VVRace
{
    public class CompProperties_AbilityFoodCost : CompProperties_AbilityEffect
    {
        public float requiredFoods;

        public CompProperties_AbilityFoodCost()
        {
            compClass = typeof(CompAbilityEffect_FoodCost);
        }
    }

    public class CompAbilityEffect_FoodCost : CompAbilityEffect
    {
        public new CompProperties_AbilityFoodCost Props => (CompProperties_AbilityFoodCost)props;

        public bool HasEnoughFood
        {
            get
            {
                var needFood = parent.pawn.needs?.food;
                if (needFood == null) { return true; }

                return needFood.CurLevelPercentage >= Props.requiredFoods;
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            var needFood = parent.pawn.needs?.food;
            if (needFood != null)
            {
                needFood.CurLevelPercentage -= Props.requiredFoods;
            }
        }

        public override void Apply(GlobalTargetInfo target)
        {
            base.Apply(target);

            var needFood = parent.pawn.needs?.food;
            if (needFood != null)
            {
                needFood.CurLevelPercentage -= Props.requiredFoods;
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (!HasEnoughFood)
            {
                reason = LocalizeString_Etc.VV_AbilityDisabledNoFood.Translate(parent.pawn);
                return true;
            }

            reason = null;
            return false;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return HasEnoughFood;
        }
    }
}
