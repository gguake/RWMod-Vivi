using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
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

            // 게임 시작시 알 부모 설정 패치
            harmony.Patch(
                original: AccessTools.Method(typeof(ScenPart_PlayerPawnsArriveMethod), nameof(ScenPart_PlayerPawnsArriveMethod.GenerateIntoMap)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(ScenPart_PlayerPawnsArriveMethod_GenerateIntoMap_Postfix)));


            // 성장 스탯 관련 패치
            harmony.Patch(
                original: AccessTools.PropertyGetter(typeof(Pawn_AgeTracker), "GrowthPointsFactor"),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Pawn_AgeTracker_get_GrowthPointsFactor_Postfix)));

            // 이복형제 방지
            harmony.Patch(
                original: AccessTools.Method(typeof(PawnRelationWorker_Sibling), nameof(PawnRelationWorker_Sibling.InRelation)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(PawnRelationWorker_Sibling_InRelation_Postfix)));

            // 말벌 MentalState 고정
            harmony.Patch(
                original: AccessTools.Method(typeof(MentalStateWorker), nameof(MentalStateWorker.StateCanOccur)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(MentalStateWorker_StateCanOccur_Postfix)));

            // 불 공포 오버라이드
            harmony.Patch(
                original: AccessTools.Method(typeof(ThoughtWorker_Pyrophobia), nameof(ThoughtWorker_Pyrophobia.NearFire)),
                prefix: new HarmonyMethod(typeof(ViviRacePatch), nameof(ThoughtWorker_Pyrophobia_NearFire_Prefix)));

            // NPC 비비 유전자 생성
            harmony.Patch(
                original: AccessTools.Method(typeof(PawnGenerator), "GenerateGenes"),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(PawnGenerator_GenerateGenes_Postfix)));

            // 비비 바닥 폐허에 안나오게 수정
            {
                var innerType = typeof(BaseGenUtility).GetNestedTypes(BindingFlags.NonPublic).FirstOrDefault(t => t.GetField("costCalculator") != null);
                var innerMehtod = innerType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
                harmony.Patch(innerMehtod, postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(BaseGenUtility_TryRandomInexpensiveFloor_Where_Postfix)));
            }

            // 벽 교체시 부착된 구조물 철거 방지
            harmony.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.DeSpawn)),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(Building_DeSpawn_Transpiler)));

            // 벽 강화 Designator 패치
            harmony.Patch(
                original: AccessTools.Method(typeof(Designator_Cancel), nameof(Designator_Cancel.DesignateThing)),
                postfix: new HarmonyMethod(typeof(ViviRacePatch), nameof(Designator_Cancel_DesignateThing_Postfix)));

            // 순수주의자 패치
            harmony.Patch(
                original: AccessTools.Method(typeof(GeneUtility), nameof(GeneUtility.AddedAndImplantedPartsWithXenogenesCount)),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(GeneUtility_AddedAndImplantedPartsWithXenogenesCount_Transpiler)));

            // 초월연결체 패치
            harmony.Patch(
                original: AccessTools.Method(typeof(MoveColonyUtility), nameof(MoveColonyUtility.MoveColonyAndReset)),
                transpiler: new HarmonyMethod(typeof(ViviRacePatch), nameof(MoveColonyUtility_MoveColonyAndReset_Transpiler)));

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
            if (previousLifeStage != null)
            {
                if ((pawn.ageTracker.CurLifeStage == VVLifeStageDefOf.HumanlikeTeenager_Vivi || 
                    pawn.ageTracker.CurLifeStage == VVLifeStageDefOf.HumanlikeTeenager || 
                    pawn.ageTracker.CurLifeStage == VVLifeStageDefOf.HumanlikeAdult_Vivi || 
                    pawn.ageTracker.CurLifeStage == LifeStageDefOf.HumanlikeAdult) && 

                    (previousLifeStage == VVLifeStageDefOf.HumanlikePreTeenager || 
                    previousLifeStage == VVLifeStageDefOf.HumanlikePreTeenager_Vivi || 
                    previousLifeStage.developmentalStage.Juvenile()))
                {
                    var compVivi = pawn.GetCompVivi();
                    if (compVivi != null)
                    {
                        compVivi.Notify_AdultLifeStageStart();
                    }
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

        private static void ScenPart_PlayerPawnsArriveMethod_GenerateIntoMap_Postfix(Map map)
        {
            if (map.IsPlayerHome && Find.GameInitData != null && Find.GameInitData.startingTile == map.Tile)
            {
                var startingRoyalVivis = Find.GameInitData.startingAndOptionalPawns?.Where(pawn => pawn.Spawned && pawn.IsRoyalVivi()).ToList();
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

        private static void Pawn_AgeTracker_get_GrowthPointsFactor_Postfix(ref float __result, Pawn ___pawn)
        {
            __result *= ___pawn.GetStatValue(VVStatDefOf.VV_GrowthPointsFactor);
        }

        private static void PawnRelationWorker_Sibling_InRelation_Postfix(ref bool __result, Pawn me, Pawn other)
        {
            if (!__result && me.IsVivi() && other.IsVivi())
            {
                __result = me.GetMother() != null && other.GetMother() != null && me.GetMother() == other.GetMother();
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

        private static bool ThoughtWorker_Pyrophobia_NearFire_Prefix(ref bool __result, Pawn pawn)
        {
            if (pawn.MapHeld != null && pawn.IsVivi())
            {
                __result = false;
                if (pawn.health?.hediffSet != null && pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_CombatHormoneJelly))
                {
                    return false;
                }

                if (pawn.IsBurning())
                {
                    __result = true;
                }
                else
                {
                    var mapHeld = pawn.MapHeld;
                    var positionHeld = pawn.PositionHeld;
                    var radiusCells = GenRadial.NumCellsInRadius(6.7f);
                    for (int i = 1; i < radiusCells; i++)
                    {
                        var cell = pawn.Position + GenRadial.RadialPattern[i];
                        if (cell.InBounds(mapHeld) && 
                            !cell.Fogged(mapHeld) &&
                            cell.ContainsStaticFire(mapHeld) &&
                            GenSight.LineOfSight(positionHeld, cell, mapHeld, skipFirstCell: true))
                        {
                            __result = true;
                            break;
                        }
                    }
                }

                return false;
            }

            return true;
        }

        private static void PawnGenerator_GenerateGenes_Postfix(Pawn pawn, XenotypeDef xenotype, ref PawnGenerationRequest request)
        {
            if (xenotype == VVXenotypeDefOf.VV_Vivi && 
                request.KindDef != null && 
                request.KindDef.race == VVThingDefOf.VV_Vivi &&
                request.KindDef.defaultFactionDef != null && 
                request.KindDef.defaultFactionDef.allowedCultures.Contains(VVCultureDefOf.VV_ViviCulture) &&
                !request.KindDef.defaultFactionDef.isPlayer &&
                pawn.genes.Xenogenes.Count == 0)
            {
                var genes = ViviUtility.SelectRandomGeneForVivi(Rand.Range(1, 2));
                foreach (var gene in genes)
                {
                    pawn.genes.AddGene(gene, true);
                }
            }
        }

        private static void BaseGenUtility_TryRandomInexpensiveFloor_Where_Postfix(ref bool __result, TerrainDef x)
        {
            if (__result && (x == VVTerrainDefOf.VV_ViviCreamFloor || x == VVTerrainDefOf.VV_HoneycombTile || x == VVTerrainDefOf.VV_SterileHoneycombTile))
            {
                __result = false;
            }
        }

        private static List<CodeInstruction> Building_DeSpawn_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();
            var injectionIndex = instructions.FindIndex(
                v => v.opcode == OpCodes.Call && 
                v.OperandIs(AccessTools.Method(typeof(ThingWithComps), nameof(ThingWithComps.DeSpawn)))) + 1;

            var jumpLabel = ilGenerator.DefineLabel();
            instructions[injectionIndex] = instructions[injectionIndex].WithLabels(jumpLabel);

            var skipIndex = instructions.FindIndex(
                v => v.opcode == OpCodes.Call && 
                v.OperandIs(AccessTools.Method(typeof(EdificeUtility), nameof(EdificeUtility.IsEdifice)))) - 2;

            var skipLabel = ilGenerator.DefineLabel();
            instructions[skipIndex] = instructions[skipIndex].WithLabels(skipLabel);

            var injection = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Bne_Un_S, jumpLabel),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Thing), nameof(Thing.def))),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VVThingDefOf), nameof(VVThingDefOf.VV_ViviCreamWall))),
                new CodeInstruction(OpCodes.Beq_S, skipLabel),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Thing), nameof(Thing.def))),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VVThingDefOf), nameof(VVThingDefOf.VV_SmoothedViviCreamWall))),
                new CodeInstruction(OpCodes.Beq_S, skipLabel),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Thing), nameof(Thing.def))),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VVThingDefOf), nameof(VVThingDefOf.VV_ViviHoneycombWall))),
                new CodeInstruction(OpCodes.Beq_S, skipLabel),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Thing), nameof(Thing.def))),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(VVThingDefOf), nameof(VVThingDefOf.VV_ViviHardenHoneycombWall))),
                new CodeInstruction(OpCodes.Beq_S, skipLabel),
            };
            instructions.InsertRange(injectionIndex, injection);
            
            return instructions;
        }

        private static void Designator_Cancel_DesignateThing_Postfix(Designator_Cancel __instance, Thing t)
        {
            if (t.def == VVThingDefOf.VV_ViviHoneycombWall)
            {
                var designation = __instance.Map.designationManager.DesignationOn(t, VVDesignationDefOf.VV_FortifyHoneycombWall);
                if (designation != null)
                {
                    __instance.Map.designationManager.RemoveDesignation(designation);
                }
            }
        }

        private static List<CodeInstruction> GeneUtility_AddedAndImplantedPartsWithXenogenesCount_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            var injectionIndex = instructions.FindIndex(
                v => v.opcode == OpCodes.Ldfld &&
                v.OperandIs(AccessTools.Field(typeof(Pawn), nameof(Pawn.genes)))) + 2;

            var labelIndex = instructions.FindIndex(v => v.opcode == OpCodes.Ret) - 1;
            var label = ilGenerator.DefineLabel();
            instructions[labelIndex] = instructions[labelIndex].WithLabels(label);

            instructions.InsertRange(injectionIndex, new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviUtility), nameof(ViviUtility.IsVivi))),
                new CodeInstruction(OpCodes.Brtrue_S, label),
            });

            return instructions;
        }

        private static void MoveColonyUtility_MoveColonyAndReset_Injection(List<Pawn> pawns)
        {
            foreach (var pawn in pawns)
            {
                var compVivi = pawn.GetCompVivi();
                if (compVivi != null)
                {
                    compVivi.Notify_LinkedEverflowerDestroyed(false);
                }
            }
        }

        private static IEnumerable<CodeInstruction> MoveColonyUtility_MoveColonyAndReset_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var instructions = codeInstructions.ToList();

            var index = instructions.FindIndex(
                v => v.opcode == OpCodes.Call &&
                v.OperandIs(AccessTools.Method(typeof(MoveColonyUtility), "RemoveWeaponsAndUtilityItems")));

            if (index >= 0)
            {
                var injectionIndex = index + 1;
                instructions.InsertRange(injectionIndex, new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldloc_S, 10),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ViviRacePatch), nameof(MoveColonyUtility_MoveColonyAndReset_Injection))),
                });
            }

            return instructions;
        }
    }
}
