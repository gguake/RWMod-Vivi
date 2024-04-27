using HarmonyLib;
using VVRace.HarmonyPatches;

namespace VVRace
{
    public static class ViviHarmonyPatcher
    {
        public static void PrePatchAll()
        {
            var harmony = new Harmony("rimworld.gguake.vivi");
            try
            {
                ViviRacePatch.Patch(harmony);
                ArcanePlantPatch.Patch(harmony);
                VanilaBugFixPatch.Patch(harmony);

                harmony.PatchAll();
            }
            finally
            {
            }
        }
    }
}
