using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public static class ViviUtility
    {
        public static bool IsVivi(this Pawn pawn) => pawn.TryGetComp<CompVivi>() != null;
        public static CompVivi GetCompVivi(this Pawn pawn) => pawn.TryGetComp<CompVivi>();
        public static CompViviEggLayer GetCompViviEggLayer(this Pawn pawn) => pawn.TryGetComp<CompViviEggLayer>();

        public static bool IsRoyalVivi(this Pawn pawn) => pawn.TryGetComp<CompVivi>()?.isRoyal ?? false;

        public static bool IsCompatibleWithVivi(GeneDef def) => def != GeneDefOf.XenogermReimplanter;

        public static List<GeneDef> SelectRandomGeneForVivi(
            int count,
            List<GeneDef> parentXenogenes = null,
            Dictionary<GeneDef, int> parentGeneInheritanceGenerations = null,
            List<GeneDef> inheritedGenes = null)
        {
            var genes = new List<GeneDef>();
            var eligibleParentGenes = parentXenogenes?
                .Where(CheckInvalidGenesForVivi)
                .Distinct()
                .ToList() ?? new List<GeneDef>();

            foreach (var def in eligibleParentGenes)
            {
                var generation = 1;
                if (parentGeneInheritanceGenerations != null &&
                    parentGeneInheritanceGenerations.TryGetValue(def, out var savedGeneration))
                {
                    generation = Mathf.Clamp(savedGeneration, 1, 3);
                }

                var inheritanceChance = generation >= 3 ? 1f : generation == 2 ? 0.8f : 0.5f;
                if (Rand.Chance(inheritanceChance))
                {
                    genes.Add(def);
                    inheritedGenes?.Add(def);
                }
            }

            if (count == 0) { return genes; }

            genes.AddRange(DefDatabase<GeneDef>.AllDefsListForReading.Where(def =>
            {
                if (genes.Contains(def) || eligibleParentGenes.Contains(def)) { return false; }

                return CheckInvalidGenesForVivi(def);

            }).ToList().TakeRandomDistinct(count));

            return genes;
        }

        public static List<GeneDef> SelectRandomArchiteGenesForVivi(int count)
        {
            if (count <= 0)
            {
                return new List<GeneDef>();
            }

            var allowModGenes = LoadedModManager.GetMod<VVRaceMod>().GetSettings<VVRaceModSettings>().allowSelectModGenes;
            var eligibleGenes = DefDatabase<GeneDef>.AllDefsListForReading.Where(def =>
            {
                if (!IsCompatibleWithVivi(def) || def.biostatArc <= 0 || !def.canGenerateInGeneSet || def.prerequisite != null)
                {
                    return false;
                }

                if (!allowModGenes && def.modContentPack != null &&
                    !def.modContentPack.PackageId.StartsWith(ModContentPack.CoreModPackageId))
                {
                    return false;
                }

                return true;

            }).ToList();

            return eligibleGenes.TakeRandomDistinct(Mathf.Min(count, eligibleGenes.Count));
        }

        private static bool CheckInvalidGenesForVivi(GeneDef def)
        {
            if (!IsCompatibleWithVivi(def)) { return false; }
            if (VVXenotypeDefOf.VV_Vivi.genes.Contains(def)) { return false; }
            if (def == GeneDefOf.Inbred) { return false; }
            if (def.biostatArc > 0 || def.displayCategory == GeneCategoryDefOf.Archite) { return false; }
            if (def.endogeneCategory != EndogeneCategory.None || def.biostatCpx == 0) { return false; }
            if (def.forcedHair != null || def.forcedHeadTypes?.Count > 0 || def.hairTagFilter != null || def.beardTagFilter != null || def.bodyType != null) { return false; }
            if (def.prerequisite != null) { return false; }
            if (def.statFactors != null && def.statFactors.Count == 1 && def.statFactors.Any(v => v.stat == StatDefOf.Fertility)) { return false; }
            if (def.statOffsets != null && def.statOffsets.Count == 1 && def.statOffsets.Any(v => v.stat == StatDefOf.Fertility)) { return false; }

            if (!LoadedModManager.GetMod<VVRaceMod>().GetSettings<VVRaceModSettings>().allowSelectModGenes)
            {
                if (def.modContentPack != null && !def.modContentPack.PackageId.StartsWith(ModContentPack.CoreModPackageId))
                {
                    return false;
                }
            }

            if (def.exclusionTags != null)
            {
                foreach (var tag in def.exclusionTags)
                {
                    switch (tag)
                    { 
                        case "HairStyle":
                        case "BeardStyle":
                        case "SkinColorOverride":
                        case "Fur":
                        case "EyeColor":
                        case "Jaw":
                            return false;
                    }
                }
            }

            return true;
        }

        private static List<HeadTypeDef> _vanillaFemaleAverageHeadsCache;

        // '바닐라 헤드만 사용' 옵션: Female_AverageNormal / Female_AveragePointy / Female_AverageWide 중 랜덤
        public static HeadTypeDef RandomVanillaFemaleAverageHead()
        {
            if (_vanillaFemaleAverageHeadsCache == null)
            {
                _vanillaFemaleAverageHeadsCache = new List<HeadTypeDef>();
                foreach (var defName in new[] { "Female_AverageNormal", "Female_AveragePointy", "Female_AverageWide" })
                {
                    var def = DefDatabase<HeadTypeDef>.GetNamedSilentFail(defName);
                    if (def != null)
                    {
                        _vanillaFemaleAverageHeadsCache.Add(def);
                    }
                }
            }

            return _vanillaFemaleAverageHeadsCache.TryRandomElement(out var result) ? result : null;
        }

        public static void GenerateHornetFromGeneticDeath(this Pawn pawn)
        {
            var map = pawn.MapHeld;
            var position = pawn.PositionHeld;
            pawn.Corpse?.Destroy();

            if (map != null && position.IsValid)
            {
                var hornet = PawnGenerator.GeneratePawn(
                    new PawnGenerationRequest(
                        VVPawnKindDefOf.VV_TitanicHornet,
                        forceGenerateNewPawn: true));
                hornet.Name = pawn.Name;

                GenSpawn.Spawn(hornet, position, map);
            }
        }
    }
}
