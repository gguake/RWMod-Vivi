using HarmonyLib;
using MonoMod.Utils;
using RimWorld;
using System;
using System.Collections;
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
                original: AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), new System.Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) }),
                prefix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_HealthTracker_AddHediff_Prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_SetFaction_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_SetFaction_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn), nameof(Pawn.DeSpawn)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_Despawn_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Pawn), nameof(Pawn.DrawAt)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_DrawAt_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(PawnGroupMaker), nameof(PawnGroupMaker.GeneratePawns)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(PawnGroupMaker_GeneratePawns_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(WorkGiver_DoBill), "StartOrResumeBillJob"),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(WorkGiver_DoBill_StartOrResumeBillJob_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(RecordsUtility), nameof(RecordsUtility.Notify_BillDone)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(RecordsUtility_Notify_BillDone_Postfix)));

            #region 연구 관련
            //harmony.Patch(
            //    original: AccessTools.Method(typeof(MainTabWindow_Architect), "CacheDesPanels"),
            //    postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(MainTabWindow_Architect_CacheDesPanels_Postfix)));

            //harmony.Patch(
            //    original: AccessTools.Method(typeof(ResearchManager), nameof(ResearchManager.FinishProject)),
            //    postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(ResearchManager_FinishProject_Postfix)));
            #endregion
        }

        private static void Pawn_NeedsTracker_ShouldHaveNeed_Postfix(ref bool __result, Pawn ___pawn, NeedDef nd)
        {
            if (nd == VVNeedDefOf.VV_RoyalJelly && !___pawn.TryGetViviGene(out _))
            {
                __result = false;
            }
        }

        private static void LifeStageWorker_HumanlikeChild_Notify_LifeStageStarted_Postfix(Pawn pawn, LifeStageDef previousLifeStage)
        {
            if (previousLifeStage != null && previousLifeStage.developmentalStage.Baby() && pawn.TryGetViviGene(out var gene))
            {
                gene.Notify_ChildLifeStageStart();
            }
        }

        private static void LifeStageWorker_HumanlikeAdult_Notify_LifeStageStarted_Postfix(Pawn pawn, LifeStageDef previousLifeStage)
        {
            if (previousLifeStage != null && previousLifeStage.developmentalStage.Juvenile() && pawn.TryGetViviGene(out var gene))
            {
                gene.Notify_AdultLifeStageStarted();
            }
        }

        private static bool Pawn_HealthTracker_AddHediff_Prefix(Hediff hediff, Pawn ___pawn)
        {
            if (hediff is Hediff_Pregnant hediff_Pregnant && ___pawn.TryGetViviGene(out var gene))
            {
                gene.Notify_PregnantHediffAdded(hediff_Pregnant);
                return false;
            }

            return true;
        }

        private static void Pawn_SetFaction_Postfix(Pawn __instance)
        {
            if (__instance.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter.Notify_ChangedFaction();
            }    
        }

        private static void Pawn_SpawnSetup_Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (__instance.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter.Notify_Spawned(respawningAfterLoad);
            }
        }

        private static void Pawn_Despawn_Postfix(Pawn __instance, DestroyMode mode)
        {
            if (__instance.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter.Notify_DeSpawned(mode);
            }
        }

        private static void Pawn_DrawAt_Postfix(Pawn __instance, Vector3 drawLoc, bool flip)
        {
            if (__instance.TryGetMindTransmitter(out var mindTransmitter))
            {
                mindTransmitter.Notify_DrawAt(drawLoc, flip);
            }
        }

        private static void PawnGroupMaker_GeneratePawns_Postfix(IEnumerable<Pawn> __result, PawnGroupMaker __instance, PawnGroupMakerParms parms)
        {
            var result = __result.ToList();
            var royalVivis = result.Where(v => v.TryGetViviGene(out var vivi) && vivi.IsRoyal && vivi.pawn.HasMindTransmitter()).ToList();

            foreach (var pawn in result)
            {
                if (royalVivis.Count() == 0) { return; }

                if (pawn.TryGetViviGene(out var vivi) && !vivi.IsRoyal)
                {
                    var i = Rand.RangeInclusive(0, royalVivis.Count() - 1);

                    var targetRoyalVivi = royalVivis[i];
                    if (targetRoyalVivi.TryGetMindTransmitter(out var mindTransmitter) && mindTransmitter.CanAddMindLink)
                    {
                        mindTransmitter.AssignPawnControl(pawn);

                        if (!mindTransmitter.CanAddMindLink)
                        {
                            royalVivis.Remove(targetRoyalVivi);
                        }
                    }
                }
            }
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
    }
}
