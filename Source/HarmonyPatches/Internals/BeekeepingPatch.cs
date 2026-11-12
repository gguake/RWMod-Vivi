using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace VVRace
{
    internal static class BeekeepingPatch
    {
        internal static void Patch(Harmony harmony)
        {
            ViviHarmonyPatcher.TryPatch(harmony, "FactionManager.FactionCanBeRemoved (beekeeping reservation)",
                AccessTools.Method(typeof(FactionManager), "FactionCanBeRemoved"),
                prefix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(FactionManager_FactionCanBeRemoved_Prefix)));

            ViviHarmonyPatcher.TryPatch(harmony, "ForbidUtility.IsForbidden (vivi nest ownership)",
                AccessTools.Method(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), new[] { typeof(Thing), typeof(Pawn) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(ForbidUtility_IsForbidden_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "ForbidUtility.IsForbidden cell (vivi nest activity area)",
                AccessTools.Method(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden), new[] { typeof(IntVec3), typeof(Pawn) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(ForbidUtility_IsForbiddenCell_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "QuestManager.Notify_ThingsProduced (vivi nest products)",
                AccessTools.Method(typeof(QuestManager), nameof(QuestManager.Notify_ThingsProduced)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(QuestManager_Notify_ThingsProduced_Postfix)));
            ViviHarmonyPatcher.TryPatch(harmony, "QuestManager.Notify_PlantHarvested (vivi nest products)",
                AccessTools.Method(typeof(QuestManager), nameof(QuestManager.Notify_PlantHarvested)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(QuestManager_Notify_PlantHarvested_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Pawn_CarryTracker.TryDropCarriedThing (vivi nest ownership)",
                AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                    new[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(System.Action<Thing, int>) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_CarryTracker_TryDropCarriedThing_Postfix)));
            ViviHarmonyPatcher.TryPatch(harmony, "Pawn_CarryTracker.TryDropCarriedThing count (vivi nest ownership)",
                AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                    new[] { typeof(IntVec3), typeof(int), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(System.Action<Thing, int>) }),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_CarryTracker_TryDropCarriedThing_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "UnfinishedThing.Creator (vivi nest ownership)",
                AccessTools.PropertySetter(typeof(UnfinishedThing), nameof(UnfinishedThing.Creator)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(UnfinishedThing_SetCreator_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "MinifyUtility.MakeMinified (vivi nest ownership)",
                AccessTools.Method(typeof(MinifyUtility), nameof(MinifyUtility.MakeMinified)),
                prefix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(MinifyUtility_MakeMinified_Prefix)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(MinifyUtility_MakeMinified_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Building.ClaimableBy (vivi nest)",
                AccessTools.Method(typeof(Building), nameof(Building.ClaimableBy)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Building_ClaimableBy_Postfix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Pawn.AdoptableBy (vivi nest baby)",
                AccessTools.Method(typeof(Pawn), nameof(Pawn.AdoptableBy)),
                postfix: new HarmonyMethod(typeof(BeekeepingPatch), nameof(Pawn_AdoptableBy_Postfix)));

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

        private static void Building_ClaimableBy_Postfix(Building __instance, Faction by, ref AcceptanceReport __result)
        {
            if (!__result.Accepted || by == null || !by.IsPlayer || !__instance.Faction.IsWanderingViviFaction()) { return; }

            if (__instance.Spawned && BeekeepingUtility.GetNestLord(__instance.Map) != null)
            {
                __result = LocalizeString_Message.VV_Message_CannotClaimViviNest.Translate().ToString();
            }
        }

        private static void Pawn_AdoptableBy_Postfix(Pawn __instance, Faction by, StringBuilder reason, ref bool __result)
        {
            if (!__result || by == null || !by.IsPlayer || __instance.GetNestLordJob() == null) { return; }

            __result = false;
            reason?.Append(LocalizeString_Message.VV_Message_CannotAdoptViviNestBaby.Translate().ToString());
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
            if (__result || pawn == null) { return; }

            if ((ThinkNode_ViviNestForbidOutsideActivityArea.IsOutsideFor(c, pawn) || JobGiver_ViviNestTakeShelter.IsOutsideShelterMealRoom(c, pawn))
                && ForbidUtility.CaresAboutForbidden(pawn, true))
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
