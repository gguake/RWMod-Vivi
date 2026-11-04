using HarmonyLib;
using RimWorld;

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
