using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse;

namespace VVRace
{
    internal static class ILUtility
    {
        internal static int FirstIndexOfInstruction(this IList<CodeInstruction> instructions, OpCode opCode, object operand = null)
        {
            if (operand != null)
            {
                return instructions.FirstIndexOf(v => v.opcode == opCode && v.OperandIs(operand));
            }
            else
            {
                return instructions.FirstIndexOf(v => v.opcode == opCode);
            }
        }

        internal static int FirstIndexOfInstruction(this IList<CodeInstruction> instructions, OpCode opCode, Predicate<CodeInstruction> predicate = null)
        {
            if (predicate != null)
            {
                return instructions.FirstIndexOf(v => v.opcode == opCode && predicate(v));
            }
            else
            {
                return instructions.FirstIndexOf(v => v.opcode == opCode);
            }
        }

        internal static int FindIndexOfLabelDestination(this IList<CodeInstruction> instructions, Label label)
        {
            for (int i = 0; i < instructions.Count; ++i)
            {
                if (instructions[i].labels.Contains(label))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
