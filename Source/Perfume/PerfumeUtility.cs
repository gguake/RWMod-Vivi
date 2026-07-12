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
        private static readonly Color OrdinaryFlowerColor = new Color(0.9f, 0.9f, 0.9f);

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
                def.plant != null &&
                def.StatBaseDefined(VVStatDefOf.VV_PlantHoneyGatherYield) &&
                def.GetStatValueAbstract(VVStatDefOf.VV_PlantHoneyGatherYield) > 0f;
        }

        public static bool IsOrdinaryFlower(ThingDef def)
        {
            return IsPerfumeFlower(def) &&
                def.GetModExtension<ArcaneFlowerPerfumeExtension>() == null;
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
            var capacityOffsets = new Dictionary<PawnCapacityDef, float>();
            var capacityFactors = new Dictionary<PawnCapacityDef, float>();
            var damageFactors = new Dictionary<DamageDef, float>();
            var totalBleedFactor = 1f;
            var hungerRateFactor = 1f;

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

                    var effectWeight = effect.scaleWithWeight ? entry.weight : 1f;

                    foreach (var modifier in effect.statOffsets ?? Enumerable.Empty<StatModifier>())
                    {
                        if (modifier.stat == null)
                        {
                            continue;
                        }

                        offsets.TryGetValue(modifier.stat, out var current);
                        offsets[modifier.stat] = current + modifier.value * effectWeight;
                    }

                    foreach (var modifier in effect.statFactors ?? Enumerable.Empty<StatModifier>())
                    {
                        if (modifier.stat == null)
                        {
                            continue;
                        }

                        MultiplyFactor(factors, modifier.stat, ScaleFactor(modifier.value, effectWeight));
                    }

                    foreach (var modifier in effect.capMods ?? Enumerable.Empty<PawnCapacityModifier>())
                    {
                        if (modifier.capacity == null)
                        {
                            continue;
                        }

                        capacityOffsets.TryGetValue(modifier.capacity, out var current);
                        capacityOffsets[modifier.capacity] = current + modifier.offset * effectWeight;
                        MultiplyFactor(
                            capacityFactors,
                            modifier.capacity,
                            ScaleFactor(modifier.postFactor, effectWeight));
                    }

                    foreach (var modifier in effect.damageFactors ?? Enumerable.Empty<DamageFactor>())
                    {
                        if (modifier.damageDef == null)
                        {
                            continue;
                        }

                        MultiplyFactor(
                            damageFactors,
                            modifier.damageDef,
                            ScaleFactor(modifier.factor, effectWeight));
                    }

                    totalBleedFactor *= ScaleFactor(effect.totalBleedFactor, effectWeight);
                    hungerRateFactor *= ScaleFactor(effect.hungerRateFactor, effectWeight);
                }
            }

            return new HediffStage
            {
                becomeVisible = true,
                statOffsets = MakeStatModifiers(offsets),
                statFactors = MakeStatModifiers(factors),
                capMods = MakeCapacityModifiers(capacityOffsets, capacityFactors),
                damageFactors = MakeDamageFactors(damageFactors),
                totalBleedFactor = totalBleedFactor,
                hungerRateFactor = hungerRateFactor
            };
        }

        private static void MultiplyFactor<T>(Dictionary<T, float> factors, T key, float factor)
        {
            if (!factors.TryGetValue(key, out var current))
            {
                current = 1f;
            }

            factors[key] = current * factor;
        }

        private static float ScaleFactor(float factor, float weight)
        {
            return 1f + (factor - 1f) * weight;
        }

        private static List<StatModifier> MakeStatModifiers(Dictionary<StatDef, float> modifiers)
        {
            return modifiers.Count == 0
                ? null
                : modifiers.OrderBy(pair => pair.Key.defName, StringComparer.Ordinal)
                    .Select(pair => new StatModifier { stat = pair.Key, value = pair.Value })
                    .ToList();
        }

        private static List<PawnCapacityModifier> MakeCapacityModifiers(
            Dictionary<PawnCapacityDef, float> offsets,
            Dictionary<PawnCapacityDef, float> factors)
        {
            return offsets.Keys
                .Concat(factors.Keys)
                .Distinct()
                .OrderBy(capacity => capacity.defName, StringComparer.Ordinal)
                .Select(capacity =>
                {
                    offsets.TryGetValue(capacity, out var offset);
                    if (!factors.TryGetValue(capacity, out var postFactor))
                    {
                        postFactor = 1f;
                    }

                    return new PawnCapacityModifier
                    {
                        capacity = capacity,
                        offset = offset,
                        postFactor = postFactor
                    };
                })
                .ToList();
        }

        private static List<DamageFactor> MakeDamageFactors(Dictionary<DamageDef, float> factors)
        {
            return factors
                .OrderBy(pair => pair.Key.defName, StringComparer.Ordinal)
                .Select(pair => new DamageFactor { damageDef = pair.Key, factor = pair.Value })
                .ToList();
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
