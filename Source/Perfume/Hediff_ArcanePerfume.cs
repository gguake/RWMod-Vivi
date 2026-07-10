using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class Hediff_ArcanePerfume : HediffWithComps
    {
        private List<ThingDef> ingredients = new List<ThingDef>();
        private float arcaneWeightPerFlower = 0.35f;
        private float ordinaryFlowerWeightBonus = 0.15f;
        private HediffStage runtimeStage;

        public override HediffStage CurStage
        {
            get
            {
                if (ingredients.NullOrEmpty())
                {
                    return base.CurStage;
                }

                if (runtimeStage == null)
                {
                    runtimeStage = PerfumeUtility.CreateStage(
                        ingredients,
                        arcaneWeightPerFlower,
                        ordinaryFlowerWeightBonus);
                }

                return runtimeStage;
            }
        }

        public override int CurStageIndex => ingredients.NullOrEmpty()
            ? 0
            : PerfumeUtility.OrdinaryFlowerCount(ingredients);

        public override string Description
        {
            get
            {
                if (ingredients.NullOrEmpty())
                {
                    return base.Description;
                }

                return base.Description + "\n\n" +
                    LocalizeString_Perfume.VV_Perfume_BlendHeader.Translate(
                        PerfumeUtility.GetBlendName(ingredients, arcaneWeightPerFlower, ordinaryFlowerWeightBonus)) +
                    "\n\n" +
                    PerfumeUtility.GetEffectDescription(ingredients, arcaneWeightPerFlower, ordinaryFlowerWeightBonus);
            }
        }

        public void SetBlend(IEnumerable<ThingDef> ingredientDefs, float arcaneWeight, float ordinaryBonus)
        {
            ingredients = ingredientDefs?.Where(def => def != null).ToList() ?? new List<ThingDef>();
            arcaneWeightPerFlower = arcaneWeight;
            ordinaryFlowerWeightBonus = ordinaryBonus;
            runtimeStage = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref ingredients, "perfumeIngredients", LookMode.Def);
            Scribe_Values.Look(ref arcaneWeightPerFlower, "perfumeArcaneWeight", 0.35f);
            Scribe_Values.Look(ref ordinaryFlowerWeightBonus, "perfumeOrdinaryBonus", 0.15f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ingredients = ingredients?.Where(def => def != null).ToList() ?? new List<ThingDef>();
                runtimeStage = null;
            }
        }
    }
}
