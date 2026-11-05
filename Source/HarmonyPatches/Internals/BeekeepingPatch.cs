using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    internal static class BeekeepingPatch
    {
        internal static void Patch(Harmony harmony)
        {
            // 정찰대가 떠난 뒤 정착대가 올 때까지 같은 임시 세력을 붙잡아 둔다.
            ViviHarmonyPatcher.TryPatch(harmony, "FactionManager.FactionCanBeRemoved (beekeeping reservation)",
                AccessTools.Method(typeof(FactionManager), "FactionCanBeRemoved"),
                prefix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(FactionManager_FactionCanBeRemoved_Prefix)));

            // 비플레이어 폰은 금지 표시를 무시하므로 둥지 비비가 플레이어 물건(식량·건설 재료)과 건물을 쓰지 않게 막는다.
            ViviHarmonyPatcher.TryPatch(harmony, "ForbidUtility.IsForbidden (vivi nest ownership)",
                AccessTools.Method(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), new[] { typeof(Thing), typeof(Pawn) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(ForbidUtility_IsForbidden_Postfix)));

            // 바닐라가 산출물을 놓기 직전에 알리는 지점: 작업·채집 지시서 산출물, 베기·수확 산출물을 둥지 몫으로 잡는다.
            ViviHarmonyPatcher.TryPatch(harmony, "QuestManager.Notify_ThingsProduced (vivi nest products)",
                AccessTools.Method(typeof(QuestManager), nameof(QuestManager.Notify_ThingsProduced)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(QuestManager_Notify_ThingsProduced_Postfix)));
            ViviHarmonyPatcher.TryPatch(harmony, "QuestManager.Notify_PlantHarvested (vivi nest products)",
                AccessTools.Method(typeof(QuestManager), nameof(QuestManager.Notify_PlantHarvested)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(QuestManager_Notify_PlantHarvested_Postfix)));

            // 바닐라가 적대 세력이 내려놓은 물건을 금지하는 지점: 둥지 비비가 내려놓은 물건(운반이 끊긴 것, 나눠 든 조각 포함)을 둥지 몫으로 남긴다.
            ViviHarmonyPatcher.TryPatch(harmony, "Pawn_CarryTracker.TryDropCarriedThing (vivi nest ownership)",
                AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                    new[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(System.Action<Thing, int>) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_CarryTracker_TryDropCarriedThing_Postfix)));
            ViviHarmonyPatcher.TryPatch(harmony, "Pawn_CarryTracker.TryDropCarriedThing count (vivi nest ownership)",
                AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                    new[] { typeof(IntVec3), typeof(int), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(System.Action<Thing, int>) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_CarryTracker_TryDropCarriedThing_Postfix)));

            // 바닐라는 플레이어 폰에게만 로드 보고를 보여 주므로 둥지 비비 정보창에 경계심 줄을 붙인다.
            ViviHarmonyPatcher.TryPatch(harmony, "Pawn.GetInspectString (vivi nest wariness)",
                AccessTools.Method(typeof(Pawn), nameof(Pawn.GetInspectString)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_GetInspectString_Postfix)));

            // 둥지 비비를 고르면 정착민이 들어가면 경계하는 둥지 방 테두리를 그린다.
            ViviHarmonyPatcher.TryPatch(harmony, "Pawn.DrawExtraSelectionOverlays (vivi nest rooms)",
                AccessTools.Method(typeof(Pawn), nameof(Pawn.DrawExtraSelectionOverlays)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_DrawExtraSelectionOverlays_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Building.ClaimableBy (vivi nest)",
                AccessTools.Method(typeof(Building), nameof(Building.ClaimableBy)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Building_ClaimableBy_Postfix)));
        }

        private static void Pawn_GetInspectString_Postfix(Pawn __instance, ref string __result)
        {
            var report = __instance.GetNestLordJob()?.GetReport(__instance);
            if (report.NullOrEmpty()) { return; }

            __result = __result.NullOrEmpty() ? report : __result + "\n" + report;
        }

        private static readonly Color NestRoomEdgeColor = new Color(1f, 0.8f, 0.35f);

        private static void Pawn_DrawExtraSelectionOverlays_Postfix(Pawn __instance)
        {
            var nest = __instance.GetNestLordJob();
            if (nest == null || !nest.Settled || __instance.Map != Find.CurrentMap) { return; }

            GenDraw.DrawFieldEdges(nest.Territory.OwnedRoomCellsForDrawing, NestRoomEdgeColor);
        }

        // 비비들이 사는 동안 둥지 건물은 차지할 수 없다(해체 가능 여부도 바닐라가 차지 가능 여부를 따른다).
        private static void Building_ClaimableBy_Postfix(Building __instance, Faction by, ref AcceptanceReport __result)
        {
            if (!__result.Accepted || by == null || !by.IsPlayer || !__instance.Faction.IsWanderingViviFaction()) { return; }

            if (__instance.Spawned && BeekeepingUtility.GetNestLord(__instance.Map) != null)
            {
                __result = LocalizeString_Message.VV_Message_CannotClaimViviNest.Translate().ToString();
            }
        }

        private static void Pawn_CarryTracker_TryDropCarriedThing_Postfix(Pawn_CarryTracker __instance, bool __result, ref Thing resultingThing)
        {
            if (__result && resultingThing != null)
            {
                __instance.pawn?.GetNestLordJob()?.MarkOwned(resultingThing);
            }
        }

        private static void QuestManager_Notify_ThingsProduced_Postfix(Pawn worker, List<Thing> things)
        {
            var nest = worker?.GetNestLordJob();
            if (nest == null || things == null) { return; }

            for (int i = 0; i < things.Count; ++i)
            {
                nest.MarkOwned(things[i]);
            }
        }

        private static void QuestManager_Notify_PlantHarvested_Postfix(Pawn worker, Thing harvested)
        {
            worker?.GetNestLordJob()?.MarkOwned(harvested);
        }

        private static void ForbidUtility_IsForbidden_Postfix(Thing t, Pawn pawn, ref bool __result)
        {
            if (__result || t == null || pawn == null) { return; }

            var faction = pawn.Faction;
            if (faction == null) { return; }

            // 정착민은 둥지 세력의 작업 자리·채집 장소 같은 건물을 쓰지 않는다(바닐라 지시서 워크기버는 세력을 보지 않는다).
            if (faction.IsPlayer)
            {
                if (t is Building nestBuilding && !(t is Building_Door) && nestBuilding.Faction != null && nestBuilding.Faction.def == VVFactionDefOf.VV_WanderingVivi)
                {
                    __result = true;
                }
                return;
            }

            if (faction.def != VVFactionDefOf.VV_WanderingVivi) { return; }

            var nest = pawn.GetNestLordJob();
            if (nest != null && !nest.CanUse(t))
            {
                __result = true;
            }
        }

        private static bool FactionManager_FactionCanBeRemoved_Prefix(Faction faction, ref bool __result)
        {
            if (BeekeepingUtility.IsFactionReservedByBeekeeping(faction))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
