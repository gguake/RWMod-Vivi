using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public static class ViviUtility
    {
        public static bool IsVivi(this Pawn pawn) => pawn.TryGetComp<CompVivi>() != null;
        public static CompVivi GetCompVivi(this Pawn pawn) => pawn.TryGetComp<CompVivi>();
        public static CompViviEggLayer GetCompViviEggLayer(this Pawn pawn) => pawn.TryGetComp<CompViviEggLayer>();

        public static bool IsRoyalVivi(this Pawn pawn) => pawn.TryGetComp<CompVivi>()?.isRoyal ?? false;

        public static List<GeneDef> SelectRandomGeneForVivi(int count, List<GeneDef> parentXenogenes = null)
        {
            var genes = new List<GeneDef>();
            if (parentXenogenes != null)
            {
                parentXenogenes.RemoveAll(def => !CheckInvalidGenesForVivi(def));

                if (parentXenogenes.Count > 0)
                {
                    var element = parentXenogenes.RandomElement();
                    parentXenogenes.Remove(element);
                    genes.Add(element);

                    foreach (var def in parentXenogenes)
                    {
                        if (Rand.Chance(0.5f))
                        {
                            genes.Add(def);
                        }
                    }
                }
            }

            if (count == 0) { return genes; }

            genes.AddRange(DefDatabase<GeneDef>.AllDefsListForReading.Where(def =>
            {
                if (genes.Contains(def)) { return false; }

                return CheckInvalidGenesForVivi(def);

            }).ToList().TakeRandomDistinct(count));

            return genes;
        }

        private static bool CheckInvalidGenesForVivi(GeneDef def)
        {
            if (VVXenotypeDefOf.VV_Vivi.genes.Contains(def)) { return false; }
            if (def.biostatArc > 0 || def.displayCategory == GeneCategoryDefOf.Archite) { return false; }
            if (def.endogeneCategory != EndogeneCategory.None || def.biostatCpx == 0) { return false; }
            if (def.forcedHair != null || def.forcedHeadTypes?.Count > 0 || def.hairTagFilter != null || def.beardTagFilter != null || def.bodyType != null) { return false; }
            if (def.prerequisite != null) { return false; }

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
