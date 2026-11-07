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

            // 둥지 생활 듀티의 일 묶음에서 일을 고르는 둥지 비비에게 둥지 활동 범위 밖 칸(과 그 칸의 물건)을 금지한다(ThinkNode_ViviNestForbidOutsideActivityArea).
            // 바닐라 듀티 지점 반경 금지(maxDistToSquadFlag)를 칠한 칸으로 바꾼 것이다. 물건 금지 판정도 놓인 칸으로 이 판정을 거친다.
            ViviHarmonyPatcher.TryPatch(harmony, "ForbidUtility.IsForbidden cell (vivi nest activity area)",
                AccessTools.Method(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), new[] { typeof(IntVec3), typeof(Pawn) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(ForbidUtility_IsForbiddenCell_Postfix)));

            // 바닐라가 산출물을 놓기 직전에 알리는 지점: 작업 지시서 산출물, 베기·수확 산출물을 둥지 몫으로 잡는다.
            ViviHarmonyPatcher.TryPatch(harmony, "QuestManager.Notify_ThingsProduced (vivi nest products)",
                AccessTools.Method(typeof(QuestManager), nameof(QuestManager.Notify_ThingsProduced)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(QuestManager_Notify_ThingsProduced_Postfix)));
            ViviHarmonyPatcher.TryPatch(harmony, "QuestManager.Notify_PlantHarvested (vivi nest products)",
                AccessTools.Method(typeof(QuestManager), nameof(QuestManager.Notify_PlantHarvested)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(QuestManager_Notify_PlantHarvested_Postfix)));

            // 바닐라가 적대 세력이 내려놓은 물건을 금지하는 지점: 둥지 비비가 내려놓은 물건(운반이 끊긴 것, 나눠 든 조각 포함)을 둥지 몫으로 남긴다.
            // 플레이어 더미에 합쳐졌으면 그 더미는 잡지 않는다(LordJob_ViviNest.MarkDropped).
            ViviHarmonyPatcher.TryPatch(harmony, "Pawn_CarryTracker.TryDropCarriedThing (vivi nest ownership)",
                AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                    new[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(System.Action<Thing, int>) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_CarryTracker_TryDropCarriedThing_Postfix)));
            ViviHarmonyPatcher.TryPatch(harmony, "Pawn_CarryTracker.TryDropCarriedThing count (vivi nest ownership)",
                AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                    new[] { typeof(IntVec3), typeof(int), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(System.Action<Thing, int>) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_CarryTracker_TryDropCarriedThing_Postfix)));

            // 둥지 비비가 만들기 시작한 미완성품(바닐라가 만든 비비를 정하고 작업 자리 칸에 바로 놓는다)을 둥지 몫으로 잡는다.
            // 잡지 않으면 금지 패치가 둥지 비비에게 감춰, 오래 걸리는 제작(새총)이 바닐라 재검토(작업 3000틱 뒤 1000틱마다)에서 끊기면
            // 시작한 비비도 이어 만들지 못한 채 놀았다(자동 테스트 p2b W8: 미완성품이 금지된 채 남은 작업 463에서 하루 넘게 멈춤).
            ViviHarmonyPatcher.TryPatch(harmony, "UnfinishedThing.Creator (vivi nest ownership)",
                AccessTools.PropertySetter(typeof(UnfinishedThing), nameof(UnfinishedThing.Creator)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(UnfinishedThing_SetCreator_Postfix)));

            // 둥지 세력 건물을 포장하면(바깥에서 자란 잔불꽃을 다시 설치 청사진으로 방 화분에 옮길 때 등) 그 포장을 둥지 몫으로 잡는다.
            // 바닐라는 포장을 그 칸에 그냥 놓으므로 잡지 않으면 둥지 비비에게 금지되고 정착민이 옮겨 간다.
            ViviHarmonyPatcher.TryPatch(harmony, "MinifyUtility.MakeMinified (vivi nest ownership)",
                AccessTools.Method(typeof(MinifyUtility), nameof(MinifyUtility.MakeMinified)),
                prefix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(MinifyUtility_MakeMinified_Prefix)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(MinifyUtility_MakeMinified_Postfix)));

            // 바닐라는 플레이어 폰에게만 로드 보고를 보여 주므로 둥지 비비 정보창에 경계심 줄을 붙인다.
            ViviHarmonyPatcher.TryPatch(harmony, "Pawn.GetInspectString (vivi nest wariness)",
                AccessTools.Method(typeof(Pawn), nameof(Pawn.GetInspectString)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_GetInspectString_Postfix)));

            // 둥지 비비를 고르면 무리의 활동 범위(일하러 오가는 칸) 테두리와 정착민이 들어가면 경계하는 둥지 방 테두리를 그린다.
            ViviHarmonyPatcher.TryPatch(harmony, "Pawn.DrawExtraSelectionOverlays (vivi nest rooms)",
                AccessTools.Method(typeof(Pawn), nameof(Pawn.DrawExtraSelectionOverlays)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_DrawExtraSelectionOverlays_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Building.ClaimableBy (vivi nest)",
                AccessTools.Method(typeof(Building), nameof(Building.ClaimableBy)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Building_ClaimableBy_Postfix)));

            // 바닐라 환자 먹이기 잡(JobDriver_FoodFeedPatient)은 환자가 누군가 먹여야 하는 상태가 아니면 바로 끝나는데, 바닐라 판정은 플레이어 세력·손님이
            // 침대에 누운 경우만 본다. 쓰러진 둥지 구성원(아기 제외)도 그 상태로 쳐 둥지 먹이기 잡(JobGiver_ViviNestFeedPatient)이 끝까지 먹이게 한다.
            // 1.7에서 이 판정은 그 잡과 바닐라 식사 잡 기버의 우선도(스스로 먹으러 가지 않음, 쓰러진 폰은 어차피 못 간다)에만 쓰인다.
            ViviHarmonyPatcher.TryPatch(harmony, "FoodUtility.ShouldBeFedBySomeone (vivi nest patient)",
                AccessTools.Method(typeof(FoodUtility), nameof(FoodUtility.ShouldBeFedBySomeone)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(FoodUtility_ShouldBeFedBySomeone_Postfix)));
        }

        private static void FoodUtility_ShouldBeFedBySomeone_Postfix(Pawn pawn, ref bool __result)
        {
            if (!__result && pawn != null && BeekeepingUtility.NeedsNestFeeding(pawn))
            {
                __result = true;
            }
        }

        private static void Pawn_GetInspectString_Postfix(Pawn __instance, ref string __result)
        {
            var report = __instance.GetNestLordJob()?.GetReport(__instance);
            if (report.NullOrEmpty()) { return; }

            __result = __result.NullOrEmpty() ? report : __result + "\n" + report;
        }

        private static readonly Color NestRoomEdgeColor = new Color(1f, 0.8f, 0.35f);
        private static readonly Color NestActivityEdgeColor = new Color(0.55f, 0.9f, 0.5f);

        private static void Pawn_DrawExtraSelectionOverlays_Postfix(Pawn __instance)
        {
            var nest = __instance.GetNestLordJob();
            if (nest == null || !nest.Settled || __instance.Map != Find.CurrentMap) { return; }

            // 활동 범위: 정착 때 둥지 중심 둘레, 증축 방마다 그 방 둘레 24칸(ViviNestTerritory)
            GenDraw.DrawFieldEdges(nest.Territory.ActivityCellsForDrawing, NestActivityEdgeColor);
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
                __instance.pawn?.GetNestLordJob()?.MarkDropped(resultingThing);
            }
        }

        private static void UnfinishedThing_SetCreator_Postfix(UnfinishedThing __instance, Pawn value)
        {
            value?.GetNestLordJob()?.MarkOwned(__instance);
        }

        // 포장하면 건물이 맵에서 빠지므로 포장 전에 맵을 기억해 둔다.
        private static void MinifyUtility_MakeMinified_Prefix(Thing thing, out Map __state)
        {
            __state = thing?.MapHeld;
        }

        private static void MinifyUtility_MakeMinified_Postfix(MinifiedThing __result, Map __state)
        {
            var faction = __result?.InnerThing?.Faction;
            if (__state == null || !faction.IsWanderingViviFaction()) { return; }

            BeekeepingUtility.GetNestLordJob(__state, faction)?.MarkOwned(__result);
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

            // 정착민은 둥지 세력의 작업 자리 같은 건물을 쓰지 않는다(바닐라 지시서 워크기버는 세력을 보지 않는다).
            if (faction.IsPlayer)
            {
                if (t is Building nestBuilding && !(t is Building_Door) && nestBuilding.Faction.IsWanderingViviFaction())
                {
                    __result = true;
                }
                return;
            }

            if (!faction.IsWanderingViviFaction()) { return; }

            var nest = pawn.GetNestLordJob();
            if (nest != null && !nest.CanUse(t))
            {
                __result = true;
            }
        }

        private static void ForbidUtility_IsForbiddenCell_Postfix(IntVec3 c, Pawn pawn, ref bool __result)
        {
            if (!__result && ThinkNode_ViviNestForbidOutsideActivityArea.IsOutsideFor(c, pawn) && ForbidUtility.CaresAboutForbidden(pawn, true))
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
