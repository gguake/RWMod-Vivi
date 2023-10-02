using HarmonyLib;
using RimWorld;
using Verse;
using VVRace.HarmonyPatches;
using VVRace.HarmonyPatches.Internals;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public static class ViviHarmonyPatcher
    {
        static ViviHarmonyPatcher()
        {
            var harmony = new Harmony("rimworld.gguake.vivi");
            try
            {
                InternalPatch.Patch(harmony);

                ApparelPropertiesExtPatch.Patch(harmony);
                ViviRacePatch.Patch(harmony);
                ArtificialPlantPatch.Patch(harmony);

                if (ModLister.GetActiveModWithIdentifier("Solaris.FurnitureBase")?.Active ?? false)
                {
                    Log.Message("!! [ViViRace] gloomyfurniture compatiblity - CanPlaceBlueprintOver unpatched");
                    harmony.Unpatch(typeof(GenConstruct).GetMethod("CanPlaceBlueprintOver"), HarmonyPatchType.Prefix, "com.Gloomylynx.rimworld.mod");
                }


                harmony.PatchAll();
            }
            finally
            {
            }
        }

        static void PawnStyleItemChooser_GetFrequencyFromIdeo_Postfix(ref float __result)
        {
            __result += float.Epsilon;
        }
    }
}
