using HarmonyLib;
using RimWorld;
using Verse;
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
                ApparelPropertiesExtPatch.Patch(harmony);
                ViviRacePatch.Patch(harmony);
                ArcanePlantPatch.Patch(harmony);

                harmony.PatchAll();
            }
            finally
            {
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class ViviHarmonyPostPatcher
    {
        public static void PostPatchAll()
        {
            var harmony = new Harmony("rimworld.gguake.vivi");
            if (ModLister.GetActiveModWithIdentifier("Solaris.FurnitureBase")?.Active ?? false)
            {
                Log.Message("!! [ViViRace] gloomyfurniture compatiblity - CanPlaceBlueprintOver unpatched");
                harmony.Unpatch(typeof(GenConstruct).GetMethod("CanPlaceBlueprintOver"), HarmonyPatchType.Prefix, "com.Gloomylynx.rimworld.mod");
            }

        }

        static ViviHarmonyPostPatcher()
        {
            PostPatchAll();
        }
    }
}
