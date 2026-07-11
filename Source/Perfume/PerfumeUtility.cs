using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace VVRace
{
    public static class PerfumeUtility
    {
        private static readonly Color OrdinaryFlowerColor = new Color(1f, 0.75f, 0.35f);

        private class BlendEntry
        {
            public ThingDef def;
            public int count;
            public float weight;
            public ArcaneFlowerPerfumeExtension arcaneExtension;
        }

        public static bool IsPerfumeFlower(ThingDef def)
        {
            return def != null &&
                (def.GetModExtension<ArcaneFlowerPerfumeExtension>() != null ||
                 def.GetModExtension<FlowerPerfumeExtension>() != null);
        }

        public static bool IsOrdinaryFlower(ThingDef def)
        {
            return def != null &&
                def.GetModExtension<ArcaneFlowerPerfumeExtension>() == null &&
                def.GetModExtension<FlowerPerfumeExtension>() != null;
        }

        public static int OrdinaryFlowerCount(IEnumerable<ThingDef> ingredients)
        {
            return ingredients?.Count(IsOrdinaryFlower) ?? 0;
        }

        public static Color GetBlendColor(IEnumerable<ThingDef> ingredients)
        {
            var ingredientList = ingredients?.Where(def => def != null).ToList() ?? new List<ThingDef>();
            if (ingredientList.Count == 0)
            {
                return Color.white;
            }

            var color = Color.clear;
            foreach (var ingredient in ingredientList)
            {
                color += GetIngredientColor(ingredient);
            }

            color /= ingredientList.Count;
            color.a = 1f;
            return color;
        }

        private static Color GetIngredientColor(ThingDef ingredient)
        {
            var extension = ingredient.GetModExtension<ArcaneFlowerPerfumeExtension>();
            var sourceColor = extension?.colorSource?.graphicData?.color;
            if (sourceColor.HasValue)
            {
                var color = sourceColor.Value;
                color.a = 1f;
                return color;
            }

            if (extension == null)
            {
                return OrdinaryFlowerColor;
            }

            var hue = (GenText.StableStringHash(ingredient.defName) & 0x7fffffff) % 1000 / 1000f;
            return Color.HSVToRGB(hue, 0.55f, 1f);
        }

        public static string GetBlendName(
            IEnumerable<ThingDef> ingredients,
            float arcaneWeightPerFlower,
            float ordinaryFlowerWeightBonus)
        {
            var entries = MakeBlendEntries(ingredients, arcaneWeightPerFlower, ordinaryFlowerWeightBonus);
            if (entries.Count == 0)
            {
                return string.Empty;
            }

            var orderedEntries = entries
                .OrderByDescending(entry => entry.weight)
                .ThenBy(entry => entry.def.defName, StringComparer.Ordinal)
                .ToList();
            var parts = orderedEntries
                .Select(GetBlendPartName)
                .ToList();
            var seed = GenText.StableStringHash(string.Join(
                "|",
                orderedEntries.Select(entry => entry.def.defName + ":" + entry.count)));
            return ResolveBlendName(parts, seed);
        }

        private static string GetBlendPartName(BlendEntry entry)
        {
            if (entry.arcaneExtension == null)
            {
                return entry.def.LabelCap.ToString();
            }

            var name = entry.arcaneExtension.GetName(entry.weight);
            return name.NullOrEmpty() ? entry.def.LabelCap.ToString() : name;
        }

        private static string ResolveBlendName(IReadOnlyList<string> parts, int seed)
        {
            var request = new GrammarRequest();
            request.Includes.Add(VVRulePackDefOf.VV_NamerPerfumeBottle);
            request.Rules.Add(new Rule_String("perfume_primary", parts[0]));
            if (parts.Count >= 2)
            {
                request.Rules.Add(new Rule_String("perfume_secondary", parts[1]));
            }
            if (parts.Count >= 3)
            {
                request.Rules.Add(new Rule_String("perfume_tertiary", parts[2]));
            }

            var rootKeyword = parts.Count >= 3
                ? "r_perfume_blend_3"
                : parts.Count == 2
                    ? "r_perfume_blend_2"
                    : "r_perfume_blend_1";
            Rand.PushState(seed);
            try
            {
                return GrammarResolver.Resolve(
                    rootKeyword,
                    request,
                    "perfume blend name",
                    capitalizeFirstSentence: false);
            }
            finally
            {
                Rand.PopState();
            }
        }

        public static HediffStage CreateStage(
            IEnumerable<ThingDef> ingredients,
            float arcaneWeightPerFlower,
            float ordinaryFlowerWeightBonus)
        {
            var offsets = new Dictionary<StatDef, float>();
            var factors = new Dictionary<StatDef, float>();

            foreach (var entry in MakeBlendEntries(ingredients, arcaneWeightPerFlower, ordinaryFlowerWeightBonus))
            {
                if (entry.arcaneExtension == null)
                {
                    continue;
                }

                foreach (var effect in entry.arcaneExtension.effects ?? Enumerable.Empty<PerfumeEffect>())
                {
                    if (entry.weight < effect.minWeight)
                    {
                        continue;
                    }

                    foreach (var modifier in effect.statOffsets ?? Enumerable.Empty<StatModifier>())
                    {
                        if (modifier.stat == null)
                        {
                            continue;
                        }

                        offsets.TryGetValue(modifier.stat, out var current);
                        offsets[modifier.stat] = current + modifier.value * entry.weight;
                    }

                    foreach (var modifier in effect.statFactors ?? Enumerable.Empty<StatModifier>())
                    {
                        if (modifier.stat == null)
                        {
                            continue;
                        }

                        factors.TryGetValue(modifier.stat, out var current);
                        if (current == 0f)
                        {
                            current = 1f;
                        }

                        var weightedFactor = 1f + (modifier.value - 1f) * entry.weight;
                        factors[modifier.stat] = current * weightedFactor;
                    }
                }
            }

            return new HediffStage
            {
                becomeVisible = true,
                statOffsets = offsets.Count == 0
                    ? null
                    : offsets.OrderBy(pair => pair.Key.defName, StringComparer.Ordinal)
                        .Select(pair => new StatModifier { stat = pair.Key, value = pair.Value })
                        .ToList(),
                statFactors = factors.Count == 0
                    ? null
                    : factors.OrderBy(pair => pair.Key.defName, StringComparer.Ordinal)
                        .Select(pair => new StatModifier { stat = pair.Key, value = pair.Value })
                        .ToList()
            };
        }

        public static string GetEffectDescription(
            IEnumerable<ThingDef> ingredients,
            float arcaneWeightPerFlower,
            float ordinaryFlowerWeightBonus)
        {
            var descriptions = MakeBlendEntries(ingredients, arcaneWeightPerFlower, ordinaryFlowerWeightBonus)
                .Where(entry => entry.arcaneExtension?.effectDescriptionKey.NullOrEmpty() == false)
                .OrderByDescending(entry => entry.weight)
                .ThenBy(entry => entry.def.defName, StringComparer.Ordinal)
                .Select(entry => entry.arcaneExtension.effectDescriptionKey.Translate().Resolve())
                .Distinct()
                .ToList();

            if (descriptions.Count == 0)
            {
                return LocalizeString_Perfume.VV_Perfume_NoArcaneEffect.Translate().Resolve();
            }

            return string.Join("\n", descriptions.Select(description => "- " + description));
        }

        private static List<BlendEntry> MakeBlendEntries(
            IEnumerable<ThingDef> ingredients,
            float arcaneWeightPerFlower,
            float ordinaryFlowerWeightBonus)
        {
            var ingredientList = ingredients?.Where(def => def != null).ToList() ?? new List<ThingDef>();
            var ordinaryCount = OrdinaryFlowerCount(ingredientList);

            return ingredientList
                .GroupBy(def => def)
                .Select(group =>
                {
                    var arcaneExtension = group.Key.GetModExtension<ArcaneFlowerPerfumeExtension>();
                    var count = group.Count();
                    return new BlendEntry
                    {
                        def = group.Key,
                        count = count,
                        arcaneExtension = arcaneExtension,
                        weight = arcaneExtension != null
                            ? count * arcaneWeightPerFlower + ordinaryCount * ordinaryFlowerWeightBonus
                            : count * ordinaryFlowerWeightBonus
                    };
                })
                .ToList();
        }
    }
}
