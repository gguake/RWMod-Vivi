using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class RecipeDef_Harvest : RecipeDef
    {
        public Type harvestWorkerType;

        [NonSerialized]
        public HarvestWorker harvestWorker;

        public Danger maxPathDanger;

        private void AddThingDefToThingFilter(ThingFilter thingFilter, IEnumerable<ThingDef> thingDefs)
        {
            var fieldInfo_thingDefs = AccessTools.Field(typeof(ThingFilter), "thingDefs");

            var listThingDef = fieldInfo_thingDefs.GetValue(thingFilter) as List<ThingDef>;
            if (listThingDef == null) { listThingDef = new List<ThingDef>(); }

            listThingDef.AddRange(thingDefs);

            fieldInfo_thingDefs.SetValue(thingFilter, listThingDef.Distinct().ToList());
        }

        public override void ResolveReferences()
        {
            if (harvestWorkerType != null)
            {
                harvestWorker = (HarvestWorker)Activator.CreateInstance(harvestWorkerType);

                var allIngredientDefs = DefDatabase<ThingDef>.AllDefsListForReading.Where(thingDef => harvestWorker.ShouldAddRecipeIngredient(thingDef));
                if (allIngredientDefs.Any())
                {
                    if (ingredients == null) { ingredients = new List<IngredientCount>(); }

                    var ingredientCount = ingredients.FirstOrDefault();
                    if (ingredientCount == null) { ingredientCount = new IngredientCount(); ingredients.Add(ingredientCount); }

                    ingredientCount.filter = new ThingFilter();
                    AddThingDefToThingFilter(ingredientCount.filter, allIngredientDefs);
                    ingredientCount.SetBaseCount(1);

                    if (fixedIngredientFilter == null) { fixedIngredientFilter = new ThingFilter(); }
                    AddThingDefToThingFilter(fixedIngredientFilter, allIngredientDefs);
                }
            }

            base.ResolveReferences();
        }
    }
}
