using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 모드 생성자에서 실행되므로 DefOf나 DefDatabase를 읽지 않는다. 패치 하나가 실패해도 나머지는 계속 적용한다.
    internal static class EndingCorePatch
    {
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

            ViviHarmonyPatcher.TryPatch(harmony, "Pawn.Kill (queen life resonance)",
                AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill)),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(Pawn_Kill_Prefix)) { priority = Priority.First });

            ViviHarmonyPatcher.TryPatch(harmony, "HediffGiver_BrainInjury.OnHediffAdded (queen life resonance)",
                AccessTools.Method(typeof(HediffGiver_BrainInjury), nameof(HediffGiver_BrainInjury.OnHediffAdded)),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(HediffGiver_BrainInjury_OnHediffAdded_Prefix)));

            ViviHarmonyPatcher.TryPatch(harmony, "TilePicker.StartTargeting_NewTemp (prevent gravship landing on ancient ruins)",
                AccessTools.Method(typeof(TilePicker), nameof(TilePicker.StartTargeting_NewTemp)),
                prefix: new HarmonyMethod(typeof(EndingCorePatch), nameof(TilePicker_StartTargeting_Prefix)));
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
                return !Hediff_EverflowerQueen.TryInterceptDeath(__instance, dinfo);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[ViViRace] failed to intercept the queen's death: {ex}", ViviEndingUtility.ErrorKeyQueenDeathIntercept);
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
            if (!ViviEndingUtility.IsThreatCategory(__instance?.def?.category)) { return; }

            var quest = ViviEndingQuest.Current;
            if (quest != null && quest.ShouldSuppressThreats(map))
            {
                __result = false;
            }
        }
    }
}
