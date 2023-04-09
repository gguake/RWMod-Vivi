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
                Harmony.DEBUG = true;
                InternalPatch.Patch(harmony);

                GeneBodyGraphicPatch.Patch(harmony);
                ApparelPropertiesExtPatch.Patch(harmony);
                MindLinkPatch.Patch(harmony);
                ViviRacePatch.Patch(harmony);

                if (ModsConfig.IdeologyActive)
                {
                    // 이데올로기 사용시 이데올로기에 따라 가중치가 0이 되는 현상 수정
                    harmony.Patch(
                        original: AccessTools.Method(typeof(PawnStyleItemChooser), "GetFrequencyFromIdeo"),
                        postfix: new HarmonyMethod(typeof(ViviHarmonyPatcher), nameof(PawnStyleItemChooser_GetFrequencyFromIdeo_Postfix)));
                }

                harmony.PatchAll();
            }
            finally
            {
                Harmony.DEBUG = false;
            }

            Harmony.DEBUG = false;
        }

        static void PawnStyleItemChooser_GetFrequencyFromIdeo_Postfix(ref float __result)
        {
            __result += float.Epsilon;
        }
    }
}
