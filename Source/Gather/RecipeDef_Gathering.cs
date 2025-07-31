using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class RecipeDef_Gathering : RecipeDef
    {
        public int GatheringWorkAmount => Mathf.FloorToInt(workAmount * gatheringWorkWeight / (gatheringWorkWeight + processingWorkWeight) + 0.5f);
        public int ProcessingWorkAmount => Mathf.FloorToInt(workAmount * processingWorkWeight / (gatheringWorkWeight + processingWorkWeight) + 0.5f);

        public Type gatherWorkerType;

        public JobDef gatheringJob;
        public int gatheringWorkWeight;
        public int processingWorkWeight;

        public StatDef targetCooldownStat;
        public StatDef targetYieldStat;

        [NonSerialized]
        public GatherWorker gatherWorker;

        public Danger maxPathDanger;

        public SimpleCurve damageChanceBySkillLevel;

        public float basePollenChance;
        public float baseArcaneSeedChance;

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
            if (gatherWorkerType != null)
            {
                gatherWorker = (GatherWorker)Activator.CreateInstance(gatherWorkerType);
                gatherWorker.recipeDef = this;

                if (targetYieldStat != null && ingredients.NullOrEmpty())
                {
                    var allTargets = DefDatabase<ThingDef>.AllDefsListForReading
                        .Where(thingDef => thingDef.StatBaseDefined(targetYieldStat) && thingDef.GetStatValueAbstract(targetYieldStat) > 0f)
                        .ToList();

                    if (allTargets.Any())
                    {
                        if (ingredients == null) { ingredients = new List<IngredientCount>(); }

                        var ingredientCount = ingredients.FirstOrDefault();
                        if (ingredientCount == null) { ingredientCount = new IngredientCount(); ingredients.Add(ingredientCount); }

                        ingredientCount.filter = new ThingFilter();
                        AddThingDefToThingFilter(ingredientCount.filter, allTargets);
                        ingredientCount.SetBaseCount(1);

                        if (fixedIngredientFilter == null) { fixedIngredientFilter = new ThingFilter(); }
                        AddThingDefToThingFilter(fixedIngredientFilter, allTargets);
                    }
                }
            }

            base.ResolveReferences();
        }
    }
}
