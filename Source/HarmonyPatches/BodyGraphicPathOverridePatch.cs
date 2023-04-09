using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VVRace.HarmonyPatches
{
    internal static class GeneBodyGraphicPatch
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(PawnGraphicSet), nameof(PawnGraphicSet.ResolveGeneGraphics)),
                postfix: new HarmonyMethod(typeof(GeneBodyGraphicPatch), nameof(PawnGraphicSet_ResolveGeneGraphics_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(PawnRenderer), nameof(PawnRenderer.BaseHeadOffsetAt)),
                transpiler: new HarmonyMethod(typeof(GeneBodyGraphicPatch), nameof(PawnRenderer_BaseHeadOffsetAt_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn_GeneTracker), "Notify_GenesChanged"),
                postfix: new HarmonyMethod(typeof(GeneBodyGraphicPatch), nameof(Pawn_GeneTracker_Notify_GenesChanged_Postfix)));
        }

        private static GeneDefExt FindBodyTypeReplacerGeneDef(Pawn pawn)
            => (GeneDefExt)pawn?.genes?.GenesListForReading.FirstOrDefault(gene => gene.def is GeneDefExt ext && ext.bodyTypeOverrides != null)?.def;

        private static Vector2 ApplyGeneBodyHeadOffset(Vector2 baseHeadOffset, Pawn pawn)
        {
            var geneDef = FindBodyTypeReplacerGeneDef(pawn);
            if (geneDef != null && pawn.story != null)
            {
                if (pawn.story.bodyType == BodyTypeDefOf.Thin)
                {
                    return baseHeadOffset + geneDef.bodyTypeOverrides.Thin.headOffset;
                }
                else if (pawn.story.bodyType == BodyTypeDefOf.Female)
                {
                    return baseHeadOffset + geneDef.bodyTypeOverrides.Female.headOffset;
                }
                else if (pawn.story.bodyType == BodyTypeDefOf.Child)
                {
                    return baseHeadOffset + geneDef.bodyTypeOverrides.Child.headOffset;
                }
            }

            return baseHeadOffset;
        }

        private static void PawnGraphicSet_ResolveGeneGraphics_Postfix(PawnGraphicSet __instance, Pawn ___pawn)
        {
            var pawn = ___pawn;
            var geneDef = FindBodyTypeReplacerGeneDef(pawn);
            if (geneDef == null || pawn.story == null) { return; }

            var shouldChangeGraphic = false;
            Color color = default;
            string path = default;

            if (pawn.story.bodyType == BodyTypeDefOf.Thin)
            {
                shouldChangeGraphic = true;
                color = (pawn.story.SkinColorOverriden ? (PawnGraphicSet.RottingColorDefault * pawn.story.SkinColor) : PawnGraphicSet.RottingColorDefault);
                path = geneDef.bodyTypeOverrides.Thin.overrideGraphicPath;
            }
            else if (pawn.story.bodyType == BodyTypeDefOf.Female)
            {
                shouldChangeGraphic = true;
                color = (pawn.story.SkinColorOverriden ? (PawnGraphicSet.RottingColorDefault * pawn.story.SkinColor) : PawnGraphicSet.RottingColorDefault);
                path = geneDef.bodyTypeOverrides.Female.overrideGraphicPath;
            }
            else if (pawn.story.bodyType == BodyTypeDefOf.Child)
            {
                shouldChangeGraphic = true;
                color = (pawn.story.SkinColorOverriden ? (PawnGraphicSet.RottingColorDefault * pawn.story.SkinColor) : PawnGraphicSet.RottingColorDefault);
                path = geneDef.bodyTypeOverrides.Child.overrideGraphicPath;
            }

            if (shouldChangeGraphic)
            {
                __instance.nakedGraphic = GraphicDatabase.Get<Graphic_Multi>(path, ShaderUtility.GetSkinShader(pawn.story.SkinColorOverriden), Vector2.one, pawn.story.SkinColor);
                __instance.rottingGraphic = GraphicDatabase.Get<Graphic_Multi>(path, ShaderUtility.GetSkinShader(pawn.story.SkinColorOverriden), Vector2.one, color);
            }
        }

        private static IEnumerable<CodeInstruction> PawnRenderer_BaseHeadOffsetAt_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var fieldHeadOffset = AccessTools.Field(typeof(BodyTypeDef), nameof(BodyTypeDef.headOffset));

            foreach (var instruction in codeInstructions)
            {
                yield return instruction;

                if (instruction.operand is FieldInfo fieldInfo && fieldInfo == fieldHeadOffset)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), "pawn"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GeneBodyGraphicPatch), nameof(ApplyGeneBodyHeadOffset)));
                }
            }
        }

        private static void Pawn_GeneTracker_Notify_GenesChanged_Postfix(Pawn ___pawn, GeneDef addedOrRemovedGene)
        {
            if (addedOrRemovedGene is GeneDefExt)
            {
                ___pawn.Drawer.renderer.graphics.SetAllGraphicsDirty();
            }
        }
    }
}
