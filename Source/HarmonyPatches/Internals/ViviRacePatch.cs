using HarmonyLib;
using RimWorld;
using System.Linq;
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
                    var radiusCells = GenRadial.NumCellsInRadius(5.7f);
                    for (int i = 1; i < radiusCells; i++)
                    {
                        var cell = pawn.Position + GenRadial.RadialPattern[i];
                        if (cell.InBounds(mapHeld) && 
                            !cell.Fogged(mapHeld) && 
                            GenSight.LineOfSight(positionHeld, cell, mapHeld, skipFirstCell: true) && 
                            cell.ContainsStaticFire(mapHeld))
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
                request.KindDef.defaultFactionType != null && 
                request.KindDef.defaultFactionType.allowedCultures.Contains(VVCultureDefOf.VV_ViviCulture) &&
                !request.KindDef.defaultFactionType.isPlayer &&
                pawn.genes.Xenogenes.Count == 0)
            {
                var genes = ViviUtility.SelectRandomGeneForVivi(Rand.Range(1, 2));
                foreach (var gene in genes)
                {
                    pawn.genes.AddGene(gene, true);
                }
            }
        }
    }
}
