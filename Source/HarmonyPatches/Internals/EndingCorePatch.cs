using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace VVRace
{
    internal static class EndingCorePatch
    {
        private static readonly FieldInfo PawnRendererResultsField = AccessTools.Field(typeof(PawnRenderer), "results");
        private static readonly FieldInfo PreRenderResultsBodyAngleField = PawnRendererResultsField != null
            ? AccessTools.Field(PawnRendererResultsField.FieldType, "bodyAngle")
            : null;
        private static readonly bool HasValidPreRenderBodyAngleField =
            PawnRendererResultsField != null &&
            PreRenderResultsBodyAngleField != null &&
            PreRenderResultsBodyAngleField.FieldType == typeof(float);
        private static readonly Action<PawnRenderer> ResetPreRenderBodyAngle = BuildResetPreRenderBodyAngle();

        private static Action<PawnRenderer> BuildResetPreRenderBodyAngle()
        {
            if (!HasValidPreRenderBodyAngleField) { return null; }
            if (!PawnRendererResultsField.FieldType.IsValueType) { return null; }

            try
            {
                var dm = new DynamicMethod(
                    "VV_ResetPreRenderBodyAngle",
                    typeof(void),
                    new[] { typeof(PawnRenderer) },
                    typeof(PawnRenderer),
                    skipVisibility: true);
                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldflda, PawnRendererResultsField);
                il.Emit(OpCodes.Ldc_R4, 0f);
                il.Emit(OpCodes.Stfld, PreRenderResultsBodyAngleField);
                il.Emit(OpCodes.Ret);
                return (Action<PawnRenderer>)dm.CreateDelegate(typeof(Action<PawnRenderer>));
            }
            catch
            {
                return null;
            }
        }

        internal static void Patch(Harmony harmony)
        {
            ViviHarmonyPatcher.TryPatch(harmony, "FactionDialogMaker.FactionDialogFor (vivi federation comms options)",
                AccessTools.Method(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor)),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(FactionDialogMaker_FactionDialogFor_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "IncidentWorker.CanFireNow (ending threat suppression)",
                AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow)),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(IncidentWorker_CanFireNow_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Fire.TrySpread (golden bloom fire containment)",
                AccessTools.Method(typeof(Fire), "TrySpread"),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(Fire_TrySpread_Prefix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Pawn.Kill (queen life resonance and apocrypha phase death)",
                AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill)),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(Pawn_Kill_Prefix)) { priority = Priority.First });

            ViviHarmonyPatcher.TryPatch(harmony, "HediffGiver_BrainInjury.OnHediffAdded (queen life resonance)",
                AccessTools.Method(typeof(HediffGiver_BrainInjury), nameof(HediffGiver_BrainInjury.OnHediffAdded)),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(HediffGiver_BrainInjury_OnHediffAdded_Prefix)));

            ViviHarmonyPatcher.TryPatch(harmony, "TilePicker.StartTargeting_NewTemp (prevent gravship landing on ancient ruins)",
                AccessTools.Method(typeof(TilePicker), nameof(TilePicker.StartTargeting_NewTemp)),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(TilePicker_StartTargeting_Prefix)));

            ViviHarmonyPatcher.TryPatch(harmony, "PawnRenderer.BodyAngle (assimilated resonator upright angle)",
                AccessTools.Method(typeof(PawnRenderer), nameof(PawnRenderer.BodyAngle)),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(PawnRenderer_BodyAngle_Postfix)) { priority = Priority.Last });

            ViviHarmonyPatcher.TryPatch(harmony, "PawnRenderer.GetDrawParms (assimilated resonator upright matrix)",
                AccessTools.Method(typeof(PawnRenderer), "GetDrawParms"),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(PawnRenderer_GetDrawParms_Prefix)) { priority = Priority.Last },
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(PawnRenderer_GetDrawParms_Postfix)) { priority = Priority.Last });

            ViviHarmonyPatcher.TryPatch(harmony, "PawnRenderer.ParallelPreRenderPawnAt (assimilated resonator cached angle)",
                AccessTools.Method(typeof(PawnRenderer), "ParallelPreRenderPawnAt"),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(PawnRenderer_ParallelPreRenderPawnAt_Postfix)) { priority = Priority.Last });

            ViviHarmonyPatcher.TryPatch(harmony, "PawnRenderNodeWorker.RotationFor (assimilated resonator node rotation)",
                AccessTools.Method(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.RotationFor)),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(PawnRenderNodeWorker_RotationFor_Postfix)) { priority = Priority.Last });

            ViviHarmonyPatcher.TryPatch(harmony, "PawnRenderNodeWorker_Head.RotationFor (assimilated resonator head rotation)",
                AccessTools.Method(typeof(PawnRenderNodeWorker_Head), nameof(PawnRenderNodeWorker_Head.RotationFor)),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(PawnRenderNodeWorker_RotationFor_Postfix)) { priority = Priority.Last });

            ViviHarmonyPatcher.TryPatch(harmony, "Pawn.Notify_Teleported (asphodel bond breaks on teleport)",
                AccessTools.Method(typeof(Pawn), nameof(Pawn.Notify_Teleported)),
                postfix: new HarmonyMethod(typeof(EndingCorePatch), nameof(Pawn_Notify_Teleported_Postfix)));
        }

        private static void Pawn_Notify_Teleported_Postfix(Pawn __instance, bool resetTweenedPos)
        {
            // 걸을 수 없는 칸에서 옆 칸으로 밀려나는 위치 보정은 resetTweenedPos=false로 오므로 순간이동으로 보지 않는다.
            if (!resetTweenedPos || !__instance.Spawned) { return; }
            if (__instance.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_AsphodelBond) == null) { return; }

            // 결속이 사라지면 체인 틱이 다음 틱에 실패 붕괴로 처리한다.
            ApocryphaFairyChains.RemoveBondHediffSilently(__instance);
            Messages.Message(LocalizeString_Message.VV_Message_AsphodelBondBrokenByTeleport.Translate(__instance.Named("PAWN")), __instance, MessageTypeDefOf.NegativeEvent);
        }

        private static float PawnRenderer_BodyAngle_Postfix(float __result, Pawn ___pawn)
        {
            return Hediff_EverflowerAssimilation.IsAssimilated(___pawn) ? 0f : __result;
        }

        private static void PawnRenderer_GetDrawParms_Prefix(Pawn ___pawn, ref float angle, ref Rot4 bodyFacing, PawnRenderFlags flags)
        {
            if (!flags.FlagSet(PawnRenderFlags.Portrait) && Hediff_EverflowerAssimilation.IsAssimilated(___pawn))
            {
                angle = 0f;
                bodyFacing = Rot4.South;
            }
        }

        private static PawnDrawParms PawnRenderer_GetDrawParms_Postfix(PawnDrawParms __result, Pawn ___pawn, Vector3 rootLoc, PawnRenderFlags flags)
        {
            if (!flags.FlagSet(PawnRenderFlags.Portrait) && Hediff_EverflowerAssimilation.IsAssimilated(___pawn))
            {
                __result.facing = Rot4.South;
                __result.posture = PawnPosture.Standing;
                __result.matrix = Matrix4x4.TRS(rootLoc + ___pawn.ageTracker.CurLifeStage.bodyDrawOffset, Quaternion.identity, Vector3.one);
            }

            return __result;
        }

        private static void PawnRenderer_ParallelPreRenderPawnAt_Postfix(PawnRenderer __instance, Pawn ___pawn)
        {
            if (!Hediff_EverflowerAssimilation.IsAssimilated(___pawn)) { return; }

            if (ResetPreRenderBodyAngle != null)
            {
                ResetPreRenderBodyAngle(__instance);
                return;
            }

            if (!HasValidPreRenderBodyAngleField) { return; }
            var boxed = PawnRendererResultsField.GetValue(__instance);
            if (boxed != null && (float)PreRenderResultsBodyAngleField.GetValue(boxed) != 0f)
            {
                PreRenderResultsBodyAngleField.SetValue(boxed, 0f);
                PawnRendererResultsField.SetValue(__instance, boxed);
            }
        }

        private static Quaternion PawnRenderNodeWorker_RotationFor_Postfix(Quaternion __result, PawnRenderNode node, PawnDrawParms parms)
        {
            if (parms.Portrait || !Hediff_EverflowerAssimilation.IsAssimilated(parms.pawn)) { return __result; }

            var tag = node?.Props?.tagDef;
            if (node == null || (node.parent != null && tag != PawnRenderNodeTagDefOf.Body && tag != PawnRenderNodeTagDefOf.Head)) { return __result; }

            var staticAngle = node.Props.drawData?.RotationOffsetForRot(parms.facing) ?? 0f;
            if (node.FlipGraphic(parms))
            {
                staticAngle = -staticAngle;
            }

            return staticAngle == 0f ? Quaternion.identity : Quaternion.AngleAxis(staticAngle, Vector3.up);
        }

        private static void TilePicker_StartTargeting_Prefix(ref Func<PlanetTile, bool> validator, bool hideFormCaravanGizmo)
        {
            if (!hideFormCaravanGizmo) { return; }

            var original = validator;
            validator = tile =>
            {
                if (Find.WorldObjects.MapParentAt(tile) is Site_ViviAncientRuins)
                {
                    Messages.Message(
                        LocalizeString_Message.VV_Message_RuinCannotLandGravship.Translate(),
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                    return false;
                }

                return original == null || original(tile);
            };
        }

        private static bool Pawn_Kill_Prefix(Pawn __instance, DamageInfo? dinfo)
        {
            try
            {
                if (Hediff_EverflowerQueen.TryInterceptDeath(__instance, dinfo))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] failed to intercept the queen's death: {ex}", ViviEndingUtility.ErrorKeyQueenDeathIntercept);
                return true;
            }

            try
            {
                return !ApocryphaUtility.IsApocrypha(__instance) || !CompApocrypha.TryInterceptKill(__instance);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] failed to intercept the apocrypha's death: {ex}", ViviEndingUtility.ErrorKeyApocryphaKillIntercept);
                return true;
            }
        }

        private static void FactionDialogMaker_FactionDialogFor_Postfix(Pawn negotiator, Faction faction, DiaNode __result)
        {
            if (__result?.options == null) { return; }

            List<DiaOption> added;
            try
            {
                added = ViviEndingCommsOptions.Make(negotiator, faction).ToList();
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] failed to make the vivi federation comms options: {ex}", ViviEndingUtility.ErrorKeyAmberCommsOption);
                return;
            }

            if (added.Count == 0) { return; }

            var options = __result.options;
            options.InsertRange(Math.Max(0, options.Count - 1), added);
        }

        private static bool HediffGiver_BrainInjury_OnHediffAdded_Prefix(Pawn pawn, Hediff hediff, ref bool __result)
        {
            if (!Hediff_EverflowerQueen.HasAnyQueen) { return true; }
            if (pawn?.health == null || hediff == null || pawn.health.hediffSet.hediffs.Contains(hediff)) { return true; }
            if (!Hediff_EverflowerQueen.IsAwaitingEnshrinement(pawn)) { return true; }

            __result = false;
            return false;
        }

        private static bool Fire_TrySpread_Prefix(Fire __instance)
        {
            return __instance.Map.weatherManager.curWeather != VVWeatherDefOf.VV_GoldenBloomMist;
        }

        private static void IncidentWorker_CanFireNow_Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
        {
            if (!__result) { return; }
            if (!(parms?.target is Map map)) { return; }

            var quest = ViviEndingQuest.Current;
            if (quest == null) { return; }

            if (quest.ShouldSuppressAllIncidents(map)
                || (ViviEndingUtility.IsThreatCategory(__instance?.def?.category) && quest.ShouldSuppressThreats(map)))
            {
                __result = false;
            }
        }
    }
}
