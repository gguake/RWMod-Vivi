using RimWorld;
using RPEF;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public static class ViviUtility
    {
        private static volatile bool _viviDefsInitialized;
        private static volatile HashSet<ThingDef> _extraViviDefs;

        [StaticConstructorOnStartup]
        private static class ViviDefCacheInitializer
        {
            static ViviDefCacheInitializer()
            {
                HashSet<ThingDef> extras = null;
                var allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
                for (int i = 0; i < allDefs.Count; i++)
                {
                    var candidate = allDefs[i];
                    if (candidate == VVThingDefOf.VV_Vivi || candidate.comps == null) { continue; }
                    for (int j = 0; j < candidate.comps.Count; j++)
                    {
                        var compClass = candidate.comps[j].compClass;
                        if (compClass != null && (compClass == typeof(CompVivi) || typeof(CompVivi).IsAssignableFrom(compClass)))
                        {
                            if (extras == null) { extras = new HashSet<ThingDef>(); }
                            extras.Add(candidate);
                            break;
                        }
                    }
                }

                _extraViviDefs = extras;
                _viviDefsInitialized = true;
            }
        }

        // StaticConstructorOnStartup 시점의 DefDatabase를 기준으로 CompVivi 보유 가능 ThingDef를 캐시한다.
        // 시작 이후 런타임에 다른 ThingDef에 CompVivi를 동적 주입하는 케이스는 지원하지 않는다.
        private static bool CanHaveCompVivi(ThingDef def)
        {
            if (def == null) { return false; }
            if (def == VVThingDefOf.VV_Vivi) { return true; }
            if (!_viviDefsInitialized) { return true; }

            var extras = _extraViviDefs;
            return extras != null && extras.Contains(def);
        }

        public static bool IsVivi(this Pawn pawn) => GetCompVivi(pawn) != null;
        public static CompVivi GetCompVivi(this Pawn pawn) => pawn != null && CanHaveCompVivi(pawn.def) ? pawn.TryGetComp<CompVivi>() : null;
        public static CompViviEggLayer GetCompViviEggLayer(this Pawn pawn) => pawn?.TryGetComp<CompViviEggLayer>();

        public static bool IsRoyalVivi(this Pawn pawn) => GetCompVivi(pawn)?.isRoyal ?? false;

        // 비비 종족의 제약(VV_PawnConstraint의 GeneConstraint)을 따른다. 유전자 추가 자체는 RPEF가 같은 제약으로 막는다.
        public static bool IsCompatibleWithVivi(GeneDef def) => def.CheckAllConstraints(VVThingDefOf.VV_Vivi, out _);

        public static bool IsModGene(GeneDef def) =>
            def.modContentPack != null &&
            !def.modContentPack.PackageId.StartsWith(ModContentPack.CoreModPackageId, StringComparison.Ordinal);

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

            var settings = VVRaceMod.Settings;
            var eligibleGenes = DefDatabase<GeneDef>.AllDefsListForReading
                .Where(def => IsArchiteGeneCandidate(def) && settings.IsGeneAllowed(def))
                .ToList();

            return eligibleGenes.TakeRandomDistinct(Mathf.Min(count, eligibleGenes.Count));
        }

        public static List<GeneDef> GetModGeneCandidates()
        {
            return DefDatabase<GeneDef>.AllDefsListForReading
                .Where(def => IsModGene(def) && (IsRandomGeneCandidate(def) || IsArchiteGeneCandidate(def)))
                .OrderBy(def => def.modContentPack.loadOrder)
                .ThenBy(def => def.label)
                .ToList();
        }

        public static bool IsArchiteGeneCandidate(GeneDef def)
        {
            return IsCompatibleWithVivi(def) && def.biostatArc > 0 && def.canGenerateInGeneSet && def.prerequisite == null;
        }

        // 저장된 부모 유전자는 모드가 제거되면 null로 로드된다.
        private static bool CheckInvalidGenesForVivi(GeneDef def)
        {
            return def != null && IsRandomGeneCandidate(def) && VVRaceMod.Settings.IsGeneAllowed(def);
        }

        public static bool IsRandomGeneCandidate(GeneDef def)
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
