using HarmonyLib;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
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

        private static IEnumerable<CodeInstruction> QuestNode_Root_Hospitality_Refugee_RunInt_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            var targetValueIndex = instructions.FirstIndexOfInstruction(OpCodes.Ldloc_S, 12) - 1;
            if (instructions[targetValueIndex].opcode == OpCodes.Ldc_R4)
            {
                instructions[targetValueIndex].operand = 100000f;
            }
            else
            {
                throw new InvalidOperationException($"failed to find injection point for QuestNode_Root_Hospitality_Refugee_RunInt_Transpiler.targetValueIndex");
            }

            return instructions;
        }
    }
}
