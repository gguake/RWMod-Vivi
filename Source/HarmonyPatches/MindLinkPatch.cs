using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace VVRace.HarmonyPatches
{
    internal static class MindLinkPatch
    {
        internal static void Patch(Harmony harmony)
        {
            MindLinkNotificationPatch(harmony);
            MindLinkCommandRangePatch(harmony);

            harmony.Patch(
                original: AccessTools.Method(typeof(PlayerPawnsDisplayOrderUtility), nameof(PlayerPawnsDisplayOrderUtility.Sort)),
                postfix: new HarmonyMethod(typeof(MindLinkPatch), nameof(PlayerPawnsDisplayOrderUtility_Sort_Postfix)));

            MethodInfo methodInfo_Pawn_DraftController_GetGizmos_MoveNext;
            #region Finding Pawn_DraftController_GetGizmos_MoveNext
            {
                var temp = PatchProcessor.GetCurrentInstructions(AccessTools.Method(typeof(Pawn_DraftController), "GetGizmos"))
                    .FirstOrDefault(op => op.opcode == OpCodes.Newobj).operand as ConstructorInfo;

                methodInfo_Pawn_DraftController_GetGizmos_MoveNext = AccessTools.Method(temp.DeclaringType, "MoveNext");
            }
            #endregion

            harmony.Patch(
                original: methodInfo_Pawn_DraftController_GetGizmos_MoveNext,
                transpiler: new HarmonyMethod(typeof(MindLinkPatch), nameof(Pawn_DraftController_GetGizmos_MoveNext_Transpiler)));

            harmony.Patch(
                original: AccessTools.PropertyGetter(typeof(MainTabWindow_Work), "Pawns"),
                postfix: new HarmonyMethod(typeof(MindLinkPatch), nameof(MainTabWindow_Pawns_get_Postfix)));

            harmony.Patch(
                original: AccessTools.PropertyGetter(typeof(MainTabWindow_Schedule), "Pawns"),
                postfix: new HarmonyMethod(typeof(MindLinkPatch), nameof(MainTabWindow_Pawns_get_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn_TimetableTracker), nameof(Pawn_TimetableTracker.SetAssignment)),
                postfix: new HarmonyMethod(typeof(MindLinkPatch), nameof(Pawn_TimetableTracker_SetAssignment_Postfix)));
        }

        private static void MindLinkNotificationPatch(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn_HealthTracker), "MakeDowned"),
                postfix: new HarmonyMethod(typeof(MindLinkPatch), nameof(Pawn_HealthTracker_MakeDowned_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Notify_ApparelChanged)),
                postfix: new HarmonyMethod(typeof(MindLinkPatch), nameof(Pawn_ApparelTracker_Notify_ApparelChanged_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus)),
                postfix: new HarmonyMethod(typeof(MindLinkPatch), nameof(Pawn_GuestTracker_SetGuestStatus_Postfix)));
        }

        private static void MindLinkCommandRangePatch(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(FloatMenuMakerMap), "AddDraftedOrders"),
                transpiler: new HarmonyMethod(typeof(MindLinkPatch), nameof(FloatMenuMakerMap_AddDraftedOrders_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(FloatMenuMakerMap), "GotoLocationOption"),
                transpiler: new HarmonyMethod(typeof(MindLinkPatch), nameof(FloatMenuMakerMap_GotoLocationOption_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(FloatMenuUtility), nameof(FloatMenuUtility.GetRangedAttackAction)),
                transpiler: new HarmonyMethod(typeof(MindLinkPatch), nameof(FloatMenuUtility_GetRangedAttackAction_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(FloatMenuUtility), nameof(FloatMenuUtility.GetMeleeAttackAction)),
                transpiler: new HarmonyMethod(typeof(MindLinkPatch), nameof(FloatMenuUtility_GetMeleeAttackAction_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(MultiPawnGotoController), "RecomputeDestinations"),
                transpiler: new HarmonyMethod(typeof(MindLinkPatch), nameof(MultiPawnGotoController_RecomputeDestinations_Transpiler)));

            MethodInfo methodInfo_MultiPawnGotoController_RecomputeDestinations_CanGoTo;
            #region Finding MultiPawnGotoController_RecomputeDestinations_CanGoTo
            {
                var temp = PatchProcessor.GetCurrentInstructions(AccessTools.Method(typeof(MultiPawnGotoController), "RecomputeDestinations"))
                    .FirstOrDefault(op => op.opcode == OpCodes.Ldftn).operand as MethodInfo;

                methodInfo_MultiPawnGotoController_RecomputeDestinations_CanGoTo = PatchProcessor.GetCurrentInstructions(temp).FirstOrDefault(op => op.opcode == OpCodes.Call).operand as MethodInfo;
            }
            #endregion

            harmony.Patch(
                original: methodInfo_MultiPawnGotoController_RecomputeDestinations_CanGoTo,
                postfix: new HarmonyMethod(typeof(MindLinkPatch), nameof(MultiPawnGotoController_RecomputeDestinations_CanGoTo_Postfix)));
        }

        internal static void PlayerPawnsDisplayOrderUtility_Sort_Postfix(List<Pawn> pawns)
        {
            pawns.RemoveAll(p => p.IsMindLinked() && pawns.Contains(p.GetMindLinkMaster()));
        }

        internal static IEnumerable<CodeInstruction> Pawn_DraftController_GetGizmos_MoveNext_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            // IL_013d: call bool Verse.ModsConfig::get_BiotechActive()
            var injectIndex = instructions.FirstIndexOfInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ModsConfig), nameof(ModsConfig.BiotechActive)));

            var conditionalSkipLabel = ilGenerator.DefineLabel();
            var injections = new CodeInstruction[]
            {
                // if (!(loc_1.pawn.HasViviGene())) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldloc_1).MoveLabelsFrom(instructions[injectIndex]),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn_DraftController), nameof(Pawn_DraftController.pawn))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviRaceUtility), nameof(ViviRaceUtility.HasViviGene))),
                new CodeInstruction(OpCodes.Brfalse_S, conditionalSkipLabel),

                // loc_3 = MindLinkUtility.CanDraftVivi(loc_1.pawn);
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn_DraftController), nameof(Pawn_DraftController.pawn))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MindLinkUtility), nameof(MindLinkUtility.CanDraftVivi))),
                new CodeInstruction(OpCodes.Stloc_3),

                // if (loc_3 == true) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldloc_3),
                new CodeInstruction(OpCodes.Call, AccessTools.GetDeclaredMethods(typeof(AcceptanceReport)).FirstOrDefault(v => v.Name == "op_Implicit" && v.ReturnType == typeof(bool))),
                new CodeInstruction(OpCodes.Brtrue_S, conditionalSkipLabel),

                // loc_2.Disable(loc_3.Reason);
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Ldloca_S, 3),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(AcceptanceReport), nameof(AcceptanceReport.Reason))),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Gizmo), nameof(Gizmo.Disable))),
            };

            instructions[injectIndex].labels.Add(conditionalSkipLabel);
            instructions.InsertRange(injectIndex, injections);
            return instructions;
        }

        internal static void MainTabWindow_Pawns_get_Postfix(ref IEnumerable<Pawn> __result)
        {
            __result = __result.Where(p => !p.IsMindLinkedVivi());
        }

        internal static void Pawn_TimetableTracker_SetAssignment_Postfix(Pawn ___pawn, int hour, TimeAssignmentDef ta)
        {
            if (___pawn.HasMindTransmitter())
            {
                ___pawn.ApplyTimeAssignmentToLinkedPawns(hour, ta);
            }
        }

        #region mind link notifications
        internal static void Pawn_HealthTracker_MakeDowned_Postfix(Pawn ___pawn)
        {
            if (___pawn.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter?.Notify_Downed();
            }
        }

        internal static void Pawn_ApparelTracker_Notify_ApparelChanged_Postfix(Pawn ___pawn)
        {
            if (___pawn.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter.Notify_ApparelChanged();
            }
        }

        internal static void Pawn_GuestTracker_SetGuestStatus_Postfix(Pawn ___pawn)
        {
            if (___pawn.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter.Notify_ChangedGuestStatus();
            }
        }
        #endregion

        #region mind link command range related
        internal static IEnumerable<CodeInstruction> FloatMenuMakerMap_AddDraftedOrders_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            // IL_0027: br IL_03ba
            var loopEndLabel = (Label)instructions.FirstOrDefault(v => v.opcode == OpCodes.Br).operand;

            // IL_0044: call bool Verse.ModsConfig::get_BiotechActive()
            var injectIndex = instructions.FirstIndexOfInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ModsConfig), nameof(ModsConfig.BiotechActive)));

            // IL_0060: ldfld valuetype Verse.LocalTargetInfo RimWorld.FloatMenuMakerMap/'<>c__DisplayClass9_1'::attackTarg
            // IL_0065: call bool MechanitorUtility::InMechanitorCommandRange(class Verse.Pawn, valuetype Verse.LocalTargetInfo)
            var inMechanitorCommandRangeMethod = AccessTools.Method(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange));
            var getAttackTargetFieldInfoOperand = instructions[instructions.FirstIndexOfInstruction(OpCodes.Call, inMechanitorCommandRangeMethod) - 1].operand;

            var conditionalSkipLabel = ilGenerator.DefineLabel();
            var injections = new CodeInstruction[]
            {
                // if (!pawn.HasViviGene()) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instructions[injectIndex]),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviRaceUtility), nameof(ViviRaceUtility.HasViviGene))),
                new CodeInstruction(OpCodes.Brfalse_S, conditionalSkipLabel),

                // if (!MindLinkUtility.InViviCommandRange(pawn, attackTarg)) { goto loopEndLabel; }
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldloc_S, 5),
                new CodeInstruction(OpCodes.Ldfld, getAttackTargetFieldInfoOperand),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MindLinkUtility), nameof(MindLinkUtility.InViviCommandRange), new Type[] { typeof(Pawn), typeof(LocalTargetInfo) })),
                new CodeInstruction(OpCodes.Brfalse, loopEndLabel),
            };

            instructions[injectIndex].labels.Add(conditionalSkipLabel);
            instructions.InsertRange(injectIndex, injections);
            return instructions;
        }

        internal static IEnumerable<CodeInstruction> FloatMenuMakerMap_GotoLocationOption_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            // IL_0065: call bool Verse.ModsConfig::get_BiotechActive()
            var injectIndex = instructions.FirstIndexOfInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ModsConfig), nameof(ModsConfig.BiotechActive)));

            // IL_0030: call valuetype Verse.IntVec3 Verse.CellFinder::StandableCellNear(valuetype Verse.IntVec3, class Verse.Map, float32)
	        // IL_0035: stfld valuetype Verse.IntVec3 RimWorld.FloatMenuMakerMap/'<>c__DisplayClass14_0'::curLoc
            var standableCellNearMethod = AccessTools.Method(typeof(CellFinder), nameof(CellFinder.StandableCellNear));
            var getAttackTargetFieldInfoOperand = instructions[instructions.FirstIndexOfInstruction(OpCodes.Call, standableCellNearMethod) + 1].operand;

            var conditionalSkipLabel = ilGenerator.DefineLabel();
            var tempFloatMenuOptionLocal = ilGenerator.DeclareLocal(typeof(FloatMenuOption));
            var injections = new CodeInstruction[]
            {
                // if (!pawn.HasViviGene()) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instructions[injectIndex]),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviRaceUtility), nameof(ViviRaceUtility.HasViviGene))),
                new CodeInstruction(OpCodes.Brfalse_S, conditionalSkipLabel),

                // var tempFloatMenuOption = GetFloatMenuOptionForGotoLocationIfMindLinked(pawn, attackTarg);
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, getAttackTargetFieldInfoOperand),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MindLinkPatch), nameof(MindLinkPatch.GetFloatMenuOptionForGotoLocationIfMindLinked))),
                new CodeInstruction(OpCodes.Stloc, tempFloatMenuOptionLocal.LocalIndex),

                // if (tempFloatMenuOption == null) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldloc, tempFloatMenuOptionLocal.LocalIndex),
                new CodeInstruction(OpCodes.Brfalse_S, conditionalSkipLabel),
                new CodeInstruction(OpCodes.Ldloc, tempFloatMenuOptionLocal.LocalIndex),

                // return;
                new CodeInstruction(OpCodes.Ret),
            };
            
            instructions[injectIndex].labels.Add(conditionalSkipLabel);
            instructions.InsertRange(injectIndex, injections);

            return instructions;
        }

        internal static IEnumerable<CodeInstruction> FloatMenuUtility_GetRangedAttackAction_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            // IL_0233: ldstr "CannotAttackSelf"
            var index = instructions.FirstIndexOfInstruction(OpCodes.Ldstr, "CannotAttackSelf");
            // IL_0248: ldloc.0
            var injectIndex = index + 5;

            // IL_0243: br IL_0364
            var skipLabel = instructions[index + 4].operand;
            var conditionalSkipLabel = ilGenerator.DefineLabel();
            var injections = new CodeInstruction[]
            {
                // if (!pawn.HasViviGene()) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instructions[injectIndex]),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviRaceUtility), nameof(ViviRaceUtility.HasViviGene))),
                new CodeInstruction(OpCodes.Brfalse_S, conditionalSkipLabel),

                // if (MindLinkUtility.InViviCommandRange(pawn, attackTarg)) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MindLinkUtility), nameof(MindLinkUtility.InViviCommandRange), new Type[] { typeof(Pawn), typeof(LocalTargetInfo) })),
                new CodeInstruction(OpCodes.Brtrue_S, conditionalSkipLabel),

                // ref arg2 = "OutOfCommandRange".Translate();
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldstr, "OutOfCommandRange"),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Translator), nameof(Translator.Translate), new System.Type[] { typeof(string) })),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TaggedString), "op_Implicit", new System.Type[] { typeof(TaggedString) })),
                new CodeInstruction(OpCodes.Stind_Ref),

                // goto skipLabel;
                new CodeInstruction(OpCodes.Br, skipLabel),
            };

            instructions[injectIndex].labels.Add(conditionalSkipLabel);
            instructions.InsertRange(injectIndex, injections);
            return instructions;
        }

        internal static IEnumerable<CodeInstruction> FloatMenuUtility_GetMeleeAttackAction_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            // IL_018d: ldstr "CannotAttackSelf"
            var index = instructions.FirstIndexOfInstruction(OpCodes.Ldstr, "CannotAttackSelf");
            // IL_01a2: ldloc.0
            var injectIndex = index + 5;

            // IL_019d: br IL_0259
            var skipLabel = instructions[index + 4].operand;
            var conditionalSkipLabel = ilGenerator.DefineLabel();
            var injections = new CodeInstruction[]
            {
                // if (!pawn.HasViviGene()) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instructions[injectIndex]),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviRaceUtility), nameof(ViviRaceUtility.HasViviGene))),
                new CodeInstruction(OpCodes.Brfalse_S, conditionalSkipLabel),

                // if (MindLinkUtility.InViviCommandRange(pawn, attackTarg)) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MindLinkUtility), nameof(MindLinkUtility.InViviCommandRange), new Type[] { typeof(Pawn), typeof(LocalTargetInfo) })),
                new CodeInstruction(OpCodes.Brtrue_S, conditionalSkipLabel),

                // ref arg2 = "OutOfCommandRange".Translate();
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldstr, "OutOfCommandRange"),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Translator), nameof(Translator.Translate), new System.Type[] { typeof(string) })),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TaggedString), "op_Implicit", new System.Type[] { typeof(TaggedString) })),
                new CodeInstruction(OpCodes.Stind_Ref),

                // goto skipIndex;
                new CodeInstruction(OpCodes.Br, skipLabel),
            };

            instructions[injectIndex].labels.Add(conditionalSkipLabel);
            instructions.InsertRange(injectIndex, injections);
            return instructions;
        }

        internal static IEnumerable<CodeInstruction> MultiPawnGotoController_RecomputeDestinations_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            // IL_00fe: call bool Verse.ModsConfig::get_BiotechActive()
            // IL_0103: brfalse.s IL_012d
            var injectIndex = instructions.FirstIndexOfInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ModsConfig), nameof(ModsConfig.BiotechActive)));
            var invalidDestinationLabel = (Label)instructions[injectIndex + 1].operand;

            // IL_011f: call bool MechanitorUtility::InMechanitorCommandRange(class Verse.Pawn, valuetype Verse.LocalTargetInfo)
            var inMechanitorCommandRangeMethod = AccessTools.Method(typeof(MechanitorUtility), nameof(MechanitorUtility.InMechanitorCommandRange));
            var inMechanitorCommandRangeMethodIndex = instructions.FirstIndexOfInstruction(OpCodes.Call, inMechanitorCommandRangeMethod);

            var conditionalSkipLabel = ilGenerator.DefineLabel();
            var injections = new CodeInstruction[]
            {
                // if (!pawn.HasViviGene()) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldloc_3).MoveLabelsFrom(instructions[injectIndex]),
                new CodeInstruction(OpCodes.Ldfld, instructions[inMechanitorCommandRangeMethodIndex - 3].operand),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviRaceUtility), nameof(ViviRaceUtility.HasViviGene))),
                new CodeInstruction(OpCodes.Brfalse_S, conditionalSkipLabel),

                // if (MindLinkUtility.InViviCommandRange(pawn, attackTarg)) { goto conditionalSkipLabel; }
                new CodeInstruction(OpCodes.Ldloc_3),
                new CodeInstruction(OpCodes.Ldfld, instructions[inMechanitorCommandRangeMethodIndex - 3].operand),
                new CodeInstruction(OpCodes.Ldloc_S, 5),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MindLinkUtility), nameof(MindLinkUtility.InViviCommandRange), new Type[] { typeof(Pawn), typeof(IntVec3) })),
                new CodeInstruction(OpCodes.Brtrue_S, conditionalSkipLabel),

                // goto invalidDestinationLabel;
                new CodeInstruction(OpCodes.Br_S, invalidDestinationLabel),
            };

            instructions[injectIndex].labels.Add(conditionalSkipLabel);
            instructions.InsertRange(injectIndex, injections);
            return instructions;
        }

        internal static void MultiPawnGotoController_RecomputeDestinations_CanGoTo_Postfix(ref bool __result, Pawn pawn, IntVec3 c)
        {
            if (pawn.HasViviGene() && !pawn.InViviCommandRange(c))
            {
                __result = false;
            }
        }

        private static FloatMenuOption GetFloatMenuOptionForGotoLocationIfMindLinked(Pawn pawn, IntVec3 curLoc)
        {
            if (pawn.IsColonist && !pawn.InViviCommandRange(curLoc))
            {
                return new FloatMenuOption("CannotGoOutOfRange".Translate() + ": " + "OutOfCommandRange".Translate(), null);
            }

            return null;
        }
        #endregion
    }
}
