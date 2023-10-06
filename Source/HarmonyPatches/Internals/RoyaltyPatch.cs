using HarmonyLib;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace VVRace
{
    internal static class RoyaltyPatch
    {
        internal static void Patch(Harmony harmony)
        {
            if (ModsConfig.RoyaltyActive)
            {
                harmony.Patch(
                    original: AccessTools.Method(typeof(QuestNode_Root_Hospitality_Refugee), "RunInt"),
                    transpiler: new HarmonyMethod(typeof(RoyaltyPatch), nameof(QuestNode_Root_Hospitality_Refugee_RunInt_Transpiler)));

                Log.Message("!! [ViViRace] royalty patch complete");
            }
        }

        private static bool IsPlayerFactionVivi()
        {
            var kindDef = Find.FactionManager.OfPlayer.def.basicMemberKind;
            return kindDef == VVPawnKindDefOf.VV_PlayerVivi || kindDef == VVPawnKindDefOf.VV_PlayerExiledRoyalVivi;
        }

        /// <summary>
        /// 	IL_05e8: ldloc.s 25
        ///     call
        ///     brtrue_s #1
        ///     IL_05ea: ldc.r4 0.25
        ///     br #2
        /// #1  ldc.r4 250.0
	    /// #2  IL_05ef: ldloc.s 12
        /// </summary>
        private static IEnumerable<CodeInstruction> QuestNode_Root_Hospitality_Refugee_RunInt_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            var targetValueIndex = instructions.FirstIndexOfInstruction(OpCodes.Ldloc_S, 12);
            if (targetValueIndex < 0)
            {
                throw new InvalidOperationException($"failed to find injection point for QuestNode_Root_Hospitality_Refugee_RunInt_Transpiler.targetValueIndex");
            }

            var label1 = ilGenerator.DefineLabel();
            var label2 = ilGenerator.DefineLabel();
            instructions[targetValueIndex].labels.Add(label2);

            instructions.InsertRange(targetValueIndex - 1, new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RoyaltyPatch), nameof(IsPlayerFactionVivi))),
                new CodeInstruction(OpCodes.Brtrue_S, label1),
            });

            targetValueIndex = instructions.FirstIndexOfInstruction(OpCodes.Ldloc_S, 12);
            instructions.InsertRange(targetValueIndex, new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Br_S, label2),
                new CodeInstruction(OpCodes.Ldc_R4, 2.5f).WithLabels(label1),
            });

            return instructions;
        }
    }
}
