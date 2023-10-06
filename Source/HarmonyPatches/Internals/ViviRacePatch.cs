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
                original: AccessTools.Method(typeof(Need_Learning), nameof(Need_Learning.Learn)),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(Need_Learning_Learn_Transpiler)));


            harmony.Patch(
                original: AccessTools.Method(typeof(PawnRelationWorker_Sibling), nameof(PawnRelationWorker_Sibling.InRelation)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(PawnRelationWorker_Sibling_InRelation_Postfix)));


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

            harmony.Patch(
                original: AccessTools.Method(typeof(PawnGraphicSet), nameof(PawnGraphicSet.ResolveAllGraphics)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(PawnGraphicSet_ResolveAllGraphics_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(PawnRenderer), "DrawHeadHair"),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(PawnRenderer_DrawHeadHair_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(HumanlikeMeshPoolUtility), nameof(HumanlikeMeshPoolUtility.GetHumanlikeHairSetForPawn)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(HumanlikeMeshPoolUtility_GetHumanlikeHairSetForPawn_Postfix)));

            Log.Message("!! [ViViRace] race patch complete");
        }

        private static void Pawn_NeedsTracker_ShouldHaveNeed_Postfix(ref bool __result, Pawn ___pawn, NeedDef nd)
        {
            if (nd == VVNeedDefOf.VV_RoyalJelly && !___pawn.IsVivi())
            {
                __result = false;
            }
        }

        private static void LifeStageWorker_HumanlikeChild_Notify_LifeStageStarted_Postfix(Pawn pawn, LifeStageDef previousLifeStage)
        {
            if (previousLifeStage != null && previousLifeStage.developmentalStage.Baby())
            {
                var compVivi = pawn.GetCompVivi();
                if (compVivi != null)
                {
                    compVivi.Notify_ChildLifeStageStart();
                }
            }
        }

        private static void LifeStageWorker_HumanlikeAdult_Notify_LifeStageStarted_Postfix(Pawn pawn, LifeStageDef previousLifeStage)
        {
            if (previousLifeStage != null && previousLifeStage.developmentalStage.Juvenile())
            {
                var compVivi = pawn.GetCompVivi();
                if (compVivi != null)
                {
                    compVivi.Notify_AdultLifeStageStart();
                }
            }
        }

        private static bool PawnGenerator_GenerateBodyType_Prefix(Pawn pawn, PawnGenerationRequest request)
        {
            var compVivi = pawn.GetCompVivi();
            if (pawn.IsVivi())
            {
                pawn.story.bodyType = BodyTypeDefOf.Thin;

                if (pawn.kindDef is PawnKindDef_Vivi kindDefExt)
                {
                    if (kindDefExt.isRoyal)
                    {
                        compVivi.SetRoyal();

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
                // if (bill.recipe == VVRecipeDefOf.VV_MakeVivicream && pawn.CanMakeViviCream())
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
                var startingRoyalVivis = Find.GameInitData.startingAndOptionalPawns?.Where(pawn => pawn.Spawned && pawn.IsRoyalVivi()).ToList();
                foreach (var vivi in startingRoyalVivis)
                {
                    vivi.GetCompVivi().SetStartingPawn();
                }

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
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VVStatDefOf), nameof(VVStatDefOf.VV_LearningRateFactor)));
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_S, -1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue)));
                    yield return new CodeInstruction(OpCodes.Mul);
                }

                yield return inst;
            }
        }

        private static IEnumerable<CodeInstruction> Need_Learning_Learn_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Need), "pawn"));
            yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VVStatDefOf), nameof(VVStatDefOf.VV_LearningRateFactor)));
            yield return new CodeInstruction(OpCodes.Ldc_I4_1);
            yield return new CodeInstruction(OpCodes.Ldc_I4_S, -1);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue)));
            yield return new CodeInstruction(OpCodes.Mul);
            yield return new CodeInstruction(OpCodes.Starg_S, 1);

            foreach (var inst in codeInstructions)
            {
                yield return inst;
            }
        }

        private static void PawnRelationWorker_Sibling_InRelation_Postfix(ref bool __result, Pawn me, Pawn other)
        {
            if (!__result && me.IsVivi() && other.IsVivi())
            {
                __result = me.GetMother() != null && other.GetMother() != null && me.GetMother() == other.GetMother();
            }
        }

        private static void Pawn_InteractionsTracker_TryInteractWith_Postfix(Pawn ___pawn, Pawn recipient, InteractionDef intDef, bool __result)
        {
            if (!__result) { return; }

            if (___pawn.IsVivi() && recipient.IsVivi() && ___pawn.Faction == recipient.Faction && !___pawn.Dead && !recipient.Dead)
            {
                Pawn giver, receiver;
                if (___pawn.IsRoyalVivi())
                {
                    giver = ___pawn;
                    receiver = recipient;
                }
                else if (recipient.IsRoyalVivi())
                {
                    giver = recipient;
                    receiver = ___pawn;
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
            if (initiator.IsVivi())
            {
                __result = 0f;
                return false;
            }

            return true;
        }

        private static bool InteractionWorker_RomanceAttempt_SuccessChance_Prefix(Pawn initiator, Pawn recipient, ref float __result)
        {
            if (recipient.IsVivi())
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
                if (ingester.IsVivi() && __result[i].thought == VVThoughtDefOf.VV_AtePollen)
                {
                    __result.RemoveAt(i);
                    i--;
                }
            }
        }

        private delegate Color SwaddleColorDelegate(PawnGraphicSet pawnGraphicSet);
        private static SwaddleColorDelegate SwaddleColor
        {
            get
            {
                if (_swaddleColorDelegate == null)
                {
                    _swaddleColorDelegate = AccessTools.Method(typeof(PawnGraphicSet), "SwaddleColor").CreateDelegate<SwaddleColorDelegate>();
                }
                return _swaddleColorDelegate;
            }
        }
        private static SwaddleColorDelegate _swaddleColorDelegate;
        private static void PawnGraphicSet_ResolveAllGraphics_Postfix(PawnGraphicSet __instance, Pawn ___pawn, ref Graphic ___swaddledBabyGraphic)
        {
            if (___pawn.IsVivi())
            {
                ___swaddledBabyGraphic = GraphicDatabase.Get<Graphic_Multi>("Things/Pawn/Vivi/Swaddle/Swaddle", ShaderDatabase.Cutout, Vector2.one, SwaddleColor(__instance));
            }
        }

        private static IEnumerable<CodeInstruction> PawnRenderer_DrawHeadHair_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var instructions = codeInstructions.ToList();

            var injectionIndex = instructions.FirstIndexOfInstruction(OpCodes.Call, AccessTools.Method(typeof(DevelopmentalStageExtensions), nameof(DevelopmentalStageExtensions.Baby))) + 1;
            var injections = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), "pawn")),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviUtility), nameof(ViviUtility.IsVivi))),
                new CodeInstruction(OpCodes.Not),
                new CodeInstruction(OpCodes.And),
            };

            instructions.InsertRange(injectionIndex, injections);
            return instructions;
        }

        private static void HumanlikeMeshPoolUtility_GetHumanlikeHairSetForPawn_Postfix(ref GraphicMeshSet __result, Pawn pawn)
        {
            if (pawn.IsVivi() && pawn.DevelopmentalStage.Baby())
            {
                __result = MeshPool.GetMeshSetForWidth(0.75f);
            }
        }
    }
}
