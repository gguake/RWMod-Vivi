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

        private static Vector2 ApplyGeneBodyHeadOffset(GeneDefExt geneDef, Pawn pawn)
        {
            GeneBodyTypeOverride bodyTypeOverride = null;

            var bodyType = pawn.story.bodyType;
            if (bodyType == BodyTypeDefOf.Thin)
            {
                bodyTypeOverride = geneDef.bodyTypeOverrides.Thin;
            }
            else if (bodyType == BodyTypeDefOf.Female)
            {
                bodyTypeOverride = geneDef.bodyTypeOverrides.Female;
            }
            else if (bodyType == BodyTypeDefOf.Child)
            {
                bodyTypeOverride = geneDef.bodyTypeOverrides.Child;
            }

            var baseHeadOffset = bodyTypeOverride != null && bodyTypeOverride.useUniqueHeadOffset ?
                bodyTypeOverride.headOffset : 
                pawn.story.bodyType.headOffset;

            return baseHeadOffset * (bodyTypeOverride != null && !bodyTypeOverride.applyLifeStageHeadOffset ? 1f : Mathf.Sqrt(pawn.ageTracker.CurLifeStage.bodySizeFactor));
        }

        private static void PawnGraphicSet_ResolveGeneGraphics_Postfix(PawnGraphicSet __instance, Pawn ___pawn)
        {
            var geneDef = FindBodyTypeReplacerGeneDef(___pawn);
            if (geneDef == null || ___pawn.story == null) { return; }

            GeneBodyTypeOverride bodyTypeOverride = null;
            if (___pawn.story.bodyType == BodyTypeDefOf.Thin)
            {
                bodyTypeOverride = geneDef.bodyTypeOverrides.Thin;
            }
            else if (___pawn.story.bodyType == BodyTypeDefOf.Female)
            {
                bodyTypeOverride = geneDef.bodyTypeOverrides.Female;
            }
            else if (___pawn.story.bodyType == BodyTypeDefOf.Child)
            {
                bodyTypeOverride = geneDef.bodyTypeOverrides.Child;
            }

            if (bodyTypeOverride?.overrideGraphicPath != null)
            {
                var color = (___pawn.story.SkinColorOverriden ? (PawnGraphicSet.RottingColorDefault * ___pawn.story.SkinColor) : PawnGraphicSet.RottingColorDefault);
                var path = bodyTypeOverride.overrideGraphicPath;

                __instance.nakedGraphic = GraphicDatabase.Get<Graphic_Multi>(path, ShaderUtility.GetSkinShader(___pawn.story.SkinColorOverriden), Vector2.one, ___pawn.story.SkinColor);
                __instance.rottingGraphic = GraphicDatabase.Get<Graphic_Multi>(path, ShaderUtility.GetSkinShader(___pawn.story.SkinColorOverriden), Vector2.one, color);
            }
        }

        private static IEnumerable<CodeInstruction> PawnRenderer_BaseHeadOffsetAt_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            var injectionIndex = 0;
            var stlocIndex = instructions.FirstIndexOfInstruction(OpCodes.Stloc_0, operand: null);

            var elseLabel = ilGenerator.DefineLabel();
            var skipLabel = ilGenerator.DefineLabel();

            var localIndex = ilGenerator.DeclareLocal(typeof(GeneDefExt));
            var injection = new List<CodeInstruction>()
            {
                // var geneDef = FindBodyTypeReplacerGeneDef(pawn);
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), "pawn")),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GeneBodyGraphicPatch), nameof(GeneBodyGraphicPatch.FindBodyTypeReplacerGeneDef))),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Stloc_S, localIndex),

                // if (geneDef == null) { goto elseLabel; }
                new CodeInstruction(OpCodes.Brfalse_S, elseLabel),

                new CodeInstruction(OpCodes.Ldloc_S, localIndex),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), "pawn")),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GeneBodyGraphicPatch), nameof(ApplyGeneBodyHeadOffset))),
                new CodeInstruction(OpCodes.Stloc_0),

                new CodeInstruction(OpCodes.Br_S, skipLabel),
            };

            instructions[injectionIndex].labels.Add(elseLabel);
            instructions[stlocIndex + 1].labels.Add(skipLabel);

            instructions.InsertRange(injectionIndex, injection);

            return instructions;
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
