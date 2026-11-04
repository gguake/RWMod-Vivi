using HarmonyLib;
using RimWorld;
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
        }

        private static void ForbidUtility_IsForbidden_Postfix(Thing t, Pawn pawn, ref bool __result)
        {
            if (__result || t == null || pawn == null) { return; }

            var faction = pawn.Faction;
            if (faction == null || faction.def != VVFactionDefOf.VV_WanderingVivi) { return; }

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
