using HarmonyLib;
using RimWorld;
using Verse;

namespace VVRace
{
    internal class VanilaBugFixPatch
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(original: AccessTools.PropertyGetter(typeof(ChoiceLetter_BabyToChild), nameof(ChoiceLetter_BabyToChild.CanShowInLetterStack)),
                prefix: new HarmonyMethod(typeof(VanilaBugFixPatch), nameof(ChoiceLetter_BabyToChild_CanShowInLetterStack_Prefix)));
        }

        private static bool ChoiceLetter_BabyToChild_CanShowInLetterStack_Prefix(ref bool __result, Pawn ___pawn)
        {
            if (___pawn == null)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
