using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class ThingDefGenerator_ArcanePlant
    {
        public static IEnumerable<ThingDef> ImpliedDefs(bool hotReload = false)
        {
            foreach (var plantDef in DefDatabase<ThingDef>.AllDefs.ToList())
            {
                if (!typeof(ArcanePlant).IsAssignableFrom(plantDef.thingClass))
                {
                    continue;
                }

                var extension = plantDef.GetModExtension<ArcaneSeedExtension>();
                if (extension == null || extension.seedDef != null) { continue; }

                var defName = $"VV_Seed_{plantDef.defName.Replace("VV_", "")}";
                var seedDef = hotReload ? DefDatabase<ThingDef>.GetNamed(defName) : new ThingDef();
                seedDef.defName = defName;
                seedDef.thingClass = typeof(ThingWithComps);
                seedDef.category = ThingCategory.Item;
                seedDef.label = LocalizeString_Etc.VV_Thing_Seed.Translate(plantDef.label);
                seedDef.description = LocalizeString_Etc.VV_Thing_SeedDesc.Translate(plantDef.label);
                seedDef.descriptionHyperlinks = new List<DefHyperlink>() { new DefHyperlink(plantDef) };

                seedDef.tickerType = TickerType.Never;
                seedDef.selectable = true;

                seedDef.graphicData = new GraphicData();
                seedDef.graphicData.graphicClass = typeof(Graphic_StackCount);
                seedDef.graphicData.texPath = "Things/Item/VV_FlowerSeed";
                seedDef.graphicData.color = extension.seedColor;

                seedDef.drawerType = DrawerType.MapMeshOnly;
                seedDef.altitudeLayer = AltitudeLayer.Item;
                seedDef.drawGUIOverlay = true;
                seedDef.rotatable = false;

                seedDef.useHitPoints = true;
                seedDef.stackLimit = 25;
                seedDef.pathCost = 14;
                seedDef.alwaysHaulable = true;

                if (extension.seedMarketValue > 0)
                {
                    seedDef.BaseMarketValue = extension.seedMarketValue;
                }
                else if (extension.seedMarketValueRatio > 0f)
                {
                    seedDef.BaseMarketValue = plantDef.BaseMarketValue * extension.seedMarketValueRatio;
                }
                else
                {
                    seedDef.BaseMarketValue = 30;
                }

                seedDef.SetStatBaseValue(StatDefOf.Mass, 0.001f);
                seedDef.SetStatBaseValue(StatDefOf.MaxHitPoints, 20f);
                seedDef.SetStatBaseValue(StatDefOf.DeteriorationRate, 0.5f);
                seedDef.SetStatBaseValue(StatDefOf.Flammability, 1f);
                seedDef.SetStatBaseValue(StatDefOf.Nutrition, 0.125f);
                seedDef.SetStatBaseValue(StatDefOf.SellPriceFactor, 0.5f);

                seedDef.ingestible = new IngestibleProperties()
                {
                    preferability = FoodPreferability.NeverForNutrition,
                    foodType = FoodTypeFlags.Seed,
                };

                seedDef.thingCategories = new List<ThingCategoryDef>();
                DirectXmlCrossRefLoader.RegisterListWantsCrossRef(seedDef.thingCategories, "VV_ArcaneSeed", seedDef);

                seedDef.comps.Clear();
                seedDef.comps.Add(new CompProperties_Forbiddable());
                seedDef.comps.Add(new CompProperties_ArcaneSeed()
                {
                    targetPlantDef = plantDef
                });

                seedDef.socialPropernessMatters = true;
                seedDef.resourceReadoutPriority = ResourceCountPriority.Middle;
                DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(seedDef, "soundInteract", "Grain_Drop");
                DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(seedDef, "soundDrop", "Grain_Drop");

                seedDef.thingSetMakerTags = new List<string>();
                seedDef.thingSetMakerTags.Add("NonStandardReward");

                seedDef.modContentPack = plantDef.modContentPack;
                extension.seedDef = seedDef;
                yield return seedDef;
            }
        }
    }
}
