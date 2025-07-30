using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    internal static class GatherPatch
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(RegionDirtyer), "SetAllClean"),
                prefix: new HarmonyMethod(typeof(GatherPatch), nameof(RegionDirtyer_SetAllClean_Prefix)));

        }

        private static bool RegionDirtyer_SetAllClean_Prefix(Map ___map, HashSet<IntVec3> ___dirtyCells)
        {
            foreach (var cell in ___dirtyCells)
            {
                ___map.GetComponent<GatheringMapComponent>().Notify_CellRegionRebuilded(cell);
            }

            return true;
        }
    }
}
