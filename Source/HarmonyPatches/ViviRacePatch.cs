using HarmonyLib;
using MonoMod.Utils;
using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace.HarmonyPatches
{
    internal class ViviRacePatch
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn_NeedsTracker), "ShouldHaveNeed"),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_NeedsTracker_ShouldHaveNeed_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(LifeStageWorker_HumanlikeChild), nameof(LifeStageWorker_HumanlikeChild.Notify_LifeStageStarted)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(LifeStageWorker_HumanlikeChild_Notify_LifeStageStarted_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(LifeStageWorker_HumanlikeAdult), nameof(LifeStageWorker_HumanlikeAdult.Notify_LifeStageStarted)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(LifeStageWorker_HumanlikeAdult_Notify_LifeStageStarted_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(PawnGenerator), "GenerateBodyType"),
                prefix: new HarmonyMethod(typeof(ViviRacePatch), nameof(PawnGenerator_GenerateBodyType_Prefix)));

            // 생산시 열량 소모 패치
            harmony.Patch(
                original: AccessTools.Method(typeof(WorkGiver_DoBill), "StartOrResumeBillJob"),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(WorkGiver_DoBill_StartOrResumeBillJob_Transpiler)));

            // 생산시 열량 소모 패치
            harmony.Patch(
                original: AccessTools.Method(typeof(RecordsUtility), nameof(RecordsUtility.Notify_BillDone)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(RecordsUtility_Notify_BillDone_Postfix)));

            // 게임 시작시 알 부모 설정 패치
            harmony.Patch(
                original: AccessTools.Method(typeof(ScenPart_PlayerPawnsArriveMethod), nameof(ScenPart_PlayerPawnsArriveMethod.GenerateIntoMap)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(ScenPart_PlayerPawnsArriveMethod_GenerateIntoMap_Postfix)));


            // 성장 스탯 관련 패치
            harmony.Patch(
                original: AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), "GrowthPointsFactor"),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_AgeTracker_get_GrowthPointsFactor_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Need_Learning), nameof(Need_Learning.NeedInterval)),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(Need_Learning_NeedInterval_Transpiler)));


            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_InteractionsTracker_TryInteractWith_Postfix)));


            harmony.Patch(
                original: AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight)),
                prefix: new HarmonyMethod(typeof(ViviRacePatch), nameof(InteractionWorker_RomanceAttempt_RandomSelectionWeight_Prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.SuccessChance)),
                prefix: new HarmonyMethod(typeof(ViviRacePatch), nameof(InteractionWorker_RomanceAttempt_SuccessChance_Prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(FoodUtility), nameof(FoodUtility.ThoughtsFromIngesting)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(FoodUtility_ThoughtsFromIngesting_Postfix)));
        }

        private static void Pawn_NeedsTracker_ShouldHaveNeed_Postfix(ref bool __result, Pawn ___pawn, NeedDef nd)
        {
            if (nd == VVNeedDefOf.VV_RoyalJelly && !(___pawn is Vivi))
            {
                __result = false;
            }
        }

        private static void LifeStageWorker_HumanlikeChild_Notify_LifeStageStarted_Postfix(Pawn pawn, LifeStageDef previousLifeStage)
        {
            if (previousLifeStage != null && previousLifeStage.developmentalStage.Baby() && pawn is Vivi vivi)
            {
                vivi.Notify_ChildLifeStageStart();
            }
        }

        private static void LifeStageWorker_HumanlikeAdult_Notify_LifeStageStarted_Postfix(Pawn pawn, LifeStageDef previousLifeStage)
        {
            if (previousLifeStage != null && previousLifeStage.developmentalStage.Juvenile() && pawn is Vivi vivi)
            {
                vivi.Notify_AdultLifeStageStart();
            }
        }

        private static bool PawnGenerator_GenerateBodyType_Prefix(Pawn pawn, PawnGenerationRequest request)
        {
            if (pawn is Vivi vivi)
            {
                pawn.story.bodyType = BodyTypeDefOf.Thin;

                if (vivi.kindDef is PawnKindDef_Vivi kindDefExt)
                {
                    if (kindDefExt.isRoyal)
                    {
                        vivi.SetRoyal();

                        if (!kindDefExt.preventRoyalBodyType)
                        {
                            pawn.story.bodyType = BodyTypeDefOf.Female;
                        }
                    }
                }

                return false;
            }

            return true;
        }

        private static Action<MainTabWindow_Architect> _MainTabWindow_Architect_CacheDesPanel = AccessTools.Method(typeof(MainTabWindow_Architect), "CacheDesPanels")
            .CreateDelegate<Action<MainTabWindow_Architect>>();

        private static void MainTabWindow_Architect_CacheDesPanels_Postfix(List<ArchitectCategoryTab> ___desPanelsCached)
        {
            var tab = ___desPanelsCached.FirstOrDefault(v => v.def == VVDesignationCategoryDefOf.VV_Bulidings);
            if (tab != null)
            {
                if (!tab.Visible)
                {
                    ___desPanelsCached.Remove(tab);
                }
            }
        }

        private static void ResearchManager_FinishProject_Postfix(ResearchProjectDef proj)
        {
            if (proj.UnlockedDefs.Any(v => v is ThingDef thingDef && thingDef.designationCategory == VVDesignationCategoryDefOf.VV_Bulidings))
            {
                _MainTabWindow_Architect_CacheDesPanel((MainTabWindow_Architect)MainButtonDefOf.Architect.TabWindow);
            }
        }

        private static IEnumerable<CodeInstruction> WorkGiver_DoBill_StartOrResumeBillJob_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            var jumpLabel = instructions[instructions.FirstIndexOfInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Bill), nameof(Bill.ShouldDoNow))) + 1].operand;
            var injectIndex = instructions.FirstIndexOfInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Bill), nameof(Bill.ShouldDoNow))) + 2;

            var conditionalSkipLabel = ilGenerator.DefineLabel();
            var injections = new CodeInstruction[]
            {
                // if (bill.recipe == VVRecipeDefOf.VV_MakeVivicream && pawn.HasViviGene())
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Bill), nameof(Bill.recipe))),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VVRecipeDefOf), nameof(VVRecipeDefOf.VV_MakeVivicream))),
                new CodeInstruction(OpCodes.Bne_Un_S, conditionalSkipLabel),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviRaceUtility), nameof(ViviRaceUtility.CanMakeViviCream))),
                new CodeInstruction(OpCodes.Brtrue_S, conditionalSkipLabel),

                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviRaceUtility), nameof(ViviRaceUtility.GetJobFailReasonForMakeViviCream))),
                new CodeInstruction(OpCodes.Ldnull),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JobFailReason), nameof(JobFailReason.Is))),

                new CodeInstruction(OpCodes.Br, jumpLabel),
            };

            instructions[injectIndex].labels.Add(conditionalSkipLabel);
            instructions.InsertRange(injectIndex, injections);
            return instructions;
        }

        private static void RecordsUtility_Notify_BillDone_Postfix(Pawn billDoer, List<Thing> products)
        {
            var bill = billDoer?.CurJob?.bill;
            if (bill != null && billDoer?.needs?.food != null)
            {
                float foodDrains = 0f;
                foreach (var product in products.Where(v => v is ThingWithComps).Cast<ThingWithComps>())
                {
                    var comp = product.TryGetComp<CompFoodDrainWhenMake>();
                    if (comp != null)
                    {
                        foodDrains += comp.Props.drainPerStackCount * product.stackCount;
                    }
                }

                billDoer.needs.food.CurLevel -= foodDrains;
            }
        }

        private static void ScenPart_PlayerPawnsArriveMethod_GenerateIntoMap_Postfix(Map map)
        {
            if (map.IsPlayerHome)
            {
                var startingRoyalVivis = Find.GameInitData.startingAndOptionalPawns?.Where(pawn => pawn.Spawned && pawn is Vivi vivi && vivi.IsRoyal).ToList();

                var allEggs = map.spawnedThings.Where(v => v.def == VVThingDefOf.VV_ViviEgg);
                foreach (var egg in allEggs)
                {
                    if (startingRoyalVivis.NullOrEmpty())
                    {
                        Log.Warning($"there is no royal vivi but vivi egg spawned");
                        return;
                    }

                    var hatcher = egg.TryGetComp<CompViviHatcher>();
                    hatcher.hatcheeParent = startingRoyalVivis.RandomElement();
                }
            }
        }

        private static IEnumerable<CodeInstruction> Pawn_AgeTracker_get_GrowthPointsFactor_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            foreach (var inst in codeInstructions)
            {
                if (inst.opcode == OpCodes.Ret)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn_AgeTracker), "pawn"));
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VVStatDefOf), nameof(VVStatDefOf.VV_GrowthPointsFactor)));
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_S, -1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue)));
                    yield return new CodeInstruction(OpCodes.Mul);
                }

                yield return inst;
            }
        }

        private static IEnumerable<CodeInstruction> Need_Learning_NeedInterval_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            foreach (var inst in codeInstructions)
            {
                if (inst.opcode == OpCodes.Sub)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Need), "pawn"));
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VVStatDefOf), nameof(VVStatDefOf.VV_LearningLossFactor)));
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_S, -1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue)));
                    yield return new CodeInstruction(OpCodes.Mul);
                }

                yield return inst;
            }
        }

        private static void Pawn_InteractionsTracker_TryInteractWith_Postfix(Pawn ___pawn, Pawn recipient, InteractionDef intDef, bool __result)
        {
            if (!__result) { return; }

            if (___pawn is Vivi viviA && recipient is Vivi viviB && viviA.Faction == viviB.Faction && !viviA.Dead && !viviB.Dead)
            {
                Vivi giver, receiver;
                if (viviA.IsRoyal)
                {
                    giver = viviA;
                    receiver = viviB;
                }
                else if (viviB.IsRoyal)
                {
                    giver = viviB;
                    receiver = viviA;
                }
                else
                {
                    return;
                }

                if (intDef.recipientThought == null || intDef.recipientThought.stages.NullOrEmpty()) { return; }

                var loyalty = receiver.needs?.TryGetNeed<Need_Loyalty>();
                if (loyalty != null)
                {
                    float baseOffset = intDef.recipientThought.stages[0].baseOpinionOffset;
                    float socialImpactMultiplier = giver.GetStatValue(StatDefOf.SocialImpact);

                    float beauty = giver.GetStatValue(StatDefOf.PawnBeauty);
                    float beautyMultiplier = Mathf.Pow(1.5f, Mathf.Abs(beauty));

                    float multiplier = socialImpactMultiplier;
                    if (baseOffset > 0f ^ beauty > 0f)
                    {
                        multiplier /= beautyMultiplier;
                    }
                    else
                    {
                        multiplier *= beautyMultiplier;
                    }

                    var value = baseOffset * multiplier;

                    loyalty.Notify_InteractWith(value);
                }
            }
        }

        private static bool InteractionWorker_RomanceAttempt_RandomSelectionWeight_Prefix(Pawn initiator, ref float __result)
        {
            if (initiator is Vivi)
            {
                __result = 0f;
                return false;
            }

            return true;
        }

        private static bool InteractionWorker_RomanceAttempt_SuccessChance_Prefix(Pawn initiator, Pawn recipient, ref float __result)
        {
            if (recipient is Vivi)
            {
                __result = 0f;
                return false;
            }

            return true;
        }

        private static void FoodUtility_ThoughtsFromIngesting_Postfix(Pawn ingester, ref List<FoodUtility.ThoughtFromIngesting> __result)
        {
            for (int i = 0; i < __result.Count; ++i)
            {
                if (ingester is Vivi && __result[i].thought == VVThoughtDefOf.VV_AtePollen)
                {
                    __result.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}
