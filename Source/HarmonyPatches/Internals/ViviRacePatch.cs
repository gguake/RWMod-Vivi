﻿using HarmonyLib;
using MonoMod.Utils;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
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
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(PawnGenerator_GenerateBodyType_Postfix)));

            // 생산시 열량 소모 패치
            harmony.Patch(
                original: AccessTools.Method(typeof(BillUtility), "MakeNewBill"),
                prefix: new HarmonyMethod(typeof(ViviRacePatch), nameof(BillUtility_MakeNewBill_Prefix)));

            // 게임 시작시 알 부모 설정 패치
            harmony.Patch(
                original: AccessTools.Method(typeof(ScenPart_PlayerPawnsArriveMethod), nameof(ScenPart_PlayerPawnsArriveMethod.GenerateIntoMap)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(ScenPart_PlayerPawnsArriveMethod_GenerateIntoMap_Postfix)));


            // 성장 스탯 관련 패치
            harmony.Patch(
                original: AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), "GrowthPointsFactor"),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_AgeTracker_get_GrowthPointsFactor_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Need_Learning), nameof(Need_Learning.Learn)),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(Need_Learning_Learn_Transpiler)));

            // 이복형제 방지
            harmony.Patch(
                original: AccessTools.Method(typeof(PawnRelationWorker_Sibling), nameof(PawnRelationWorker_Sibling.InRelation)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(PawnRelationWorker_Sibling_InRelation_Postfix)));


            // 로맨스 불가 - 가중치 0
            harmony.Patch(
                original: AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight)),
                prefix: new HarmonyMethod(typeof(ViviRacePatch), nameof(InteractionWorker_RomanceAttempt_RandomSelectionWeight_Prefix)));

            // 로맨스 불가 - 성공률 0
            harmony.Patch(
                original: AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.SuccessChance)),
                prefix: new HarmonyMethod(typeof(ViviRacePatch), nameof(InteractionWorker_RomanceAttempt_SuccessChance_Prefix)));

            // TODO: CHECK
            harmony.Patch(
                original: AccessTools.Method(typeof(HumanlikeMeshPoolUtility), nameof(HumanlikeMeshPoolUtility.GetHumanlikeHairSetForPawn)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(HumanlikeMeshPoolUtility_GetHumanlikeHairSetForPawn_Postfix)));

            // 정착지 변경
            harmony.Patch(
                original: AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.MapGeneratorDef)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Settlement_MapGeneratorDef_getter_Postfix)));

            // 말벌 MentalState 고정
            harmony.Patch(
                original: AccessTools.Method(typeof(MentalStateWorker), nameof(MentalStateWorker.StateCanOccur)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(MentalStateWorker_StateCanOccur_Postfix)));

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

        private static void PawnGenerator_GenerateBodyType_Postfix(Pawn pawn, PawnGenerationRequest request)
        {
            if (!pawn.DevelopmentalStage.Adult()) { return; }

            if (pawn.kindDef is PawnKindDef_Vivi kindDefExt)
            {
                if (kindDefExt.isRoyal)
                {
                    var compVivi = pawn.GetCompVivi();
                    if (compVivi != null)
                    {
                        compVivi.SetRoyal();
                    }
                }

                if (kindDefExt.forcedBodyType != null)
                {
                    pawn.story.bodyType = kindDefExt.forcedBodyType;
                }
            }
        }

        private static bool BillUtility_MakeNewBill_Prefix(ref Bill __result, RecipeDef recipe, Precept_ThingStyle precept)
        {
            var extension = recipe.GetModExtension<RecipeModExtension>();
            if (extension == null) { return true; }

            if (extension.billType != null)
            {
                if (!typeof(Bill).IsAssignableFrom(extension.billType))
                {
                    Log.Error($"invalid billtype for recipe mod extension");
                    return true;
                }

                var bill = Activator.CreateInstance(extension.billType, new object[] { recipe, precept }) as Bill;
                if (bill != null)
                {
                    __result = bill;
                    return false;
                }
            }

            return true;
        }

        private static void ScenPart_PlayerPawnsArriveMethod_GenerateIntoMap_Postfix(Map map)
        {
            if (map.IsPlayerHome && Find.GameInitData.startingTile == map.Tile)
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

        private static void HumanlikeMeshPoolUtility_GetHumanlikeHairSetForPawn_Postfix(ref GraphicMeshSet __result, Pawn pawn)
        {
            if (pawn.IsVivi() && pawn.DevelopmentalStage.Baby())
            {
                __result = MeshPool.GetMeshSetForWidth(0.75f);
            }
        }

        private static void Settlement_MapGeneratorDef_getter_Postfix(ref MapGeneratorDef __result, Settlement __instance)
        {
            if (!__instance.Faction.IsPlayer && __instance.Faction.def.allowedCultures.Contains(VVCultureDefOf.VV_ViviCulture))
            {
                __result = VVMapGeneratorDefOf.VV_Base_ViviFaction;
            }
        }

        private static void MentalStateWorker_StateCanOccur_Postfix(ref bool __result, Pawn pawn, MentalStateDef ___def)
        {
            if (__result && pawn.InMentalState)
            {
                if (___def == MentalStateDefOf.Manhunter || ___def == MentalStateDefOf.ManhunterPermanent)
                {
                    if (pawn.MentalStateDef == VVMentalStateDefOf.VV_HornetBerserk)
                    {
                        __result = false;
                    }
                }
            }
        }
    }
}
