using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace VVRace.HarmonyPatches
{
    internal static class ApparelPropertiesExtPatch
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(ApparelProperties), nameof(ApparelProperties.PawnCanWear), new Type[] { typeof(Pawn), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(ApparelPropertiesExtPatch), nameof(ApparelProperties_PawnCanWear_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip), new Type[] { typeof(Thing), typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(ApparelPropertiesExtPatch), nameof(EquipmentUtility_CanEquip_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(ApparelGraphicRecordGetter), nameof(ApparelGraphicRecordGetter.TryGetGraphicApparel)),
                prefix: new HarmonyMethod(typeof(ApparelPropertiesExtPatch), nameof(ApparelGraphicRecordGetter_TryGetGraphicApparel_Prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.ThoughtNullified)),
                postfix: new HarmonyMethod(typeof(ApparelPropertiesExtPatch), nameof(ThoughtUtility_ThoughtNullified_Postfix)));
        }

        private static void ApparelProperties_PawnCanWear_Postfix(ref bool __result, ApparelProperties __instance, Pawn pawn)
        {
            if (!__result) { return; }

            __result = CanWearWithBodyType(pawn, __instance);
        }

        private static void EquipmentUtility_CanEquip_Postfix(ref bool __result, Thing thing, Pawn pawn, ref string cantReason)
        {
            if (!__result) { return; }

            if (thing.def.IsApparel && !CanWearWithBodyType(pawn, thing.def.apparel))
            {
                cantReason = LocalizeTexts.ApparelBodyTypeWhitelisted.Translate(
                    pawn.DevelopmentalStage.ToString().Translate(),
                    Find.ActiveLanguageWorker.WithIndefiniteArticlePostProcessed(thing.def.apparel.developmentalStageFilter.ToCommaListOr()));

                __result = false;
            }
        }

        private static bool CanWearWithBodyType(Pawn pawn, ApparelProperties apparelProperties)
        {
            if (apparelProperties is ApparelPropertiesExt props)
            {
                if (!props.bodyTypeWhitelist.NullOrEmpty() && pawn.story?.bodyType != null)
                {
                    return props.bodyTypeWhitelist.Contains(pawn.story.bodyType);
                }
            }

            return true;
        }

        private static bool ApparelGraphicRecordGetter_TryGetGraphicApparel_Prefix(Apparel apparel, ref BodyTypeDef bodyType)
        {
            if (apparel.def.apparel is ApparelPropertiesExt apparelPropertiesExt && apparelPropertiesExt.fixedBodyType != null)
            {
                bodyType = apparelPropertiesExt.fixedBodyType;
            }

            return true;
        }

        // TODO
        private static void ThoughtUtility_ThoughtNullified_Postfix(ref bool __result, Pawn pawn, ThoughtDef def)
        {
            if (__result || pawn.apparel?.WornApparel == null) { return; }

            foreach (var apparel in pawn.apparel.WornApparel)
            {
                if (apparel.def.apparel is ApparelPropertiesExt apparelPropertiesExt && 
                    apparelPropertiesExt.nullifyingThoughts != null && 
                    apparelPropertiesExt.nullifyingThoughts.Contains(def))
                {
                    __result = true;
                    return;
                }
            }
        }
    }
}
