using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    internal class VanilaBugFixPatch
    {
        internal static void Patch(Harmony harmony)
        {
            ViviHarmonyPatcher.TryPatch(harmony, "ChoiceLetter_BabyToChild.CanShowInLetterStack (null pawn letter fix)",
                AccessTools.PropertyGetter(typeof(ChoiceLetter_BabyToChild), nameof(ChoiceLetter_BabyToChild.CanShowInLetterStack)),
                prefix: new HarmonyMethod(typeof(VanilaBugFixPatch), nameof(ChoiceLetter_BabyToChild_CanShowInLetterStack_Prefix)));

            ViviHarmonyPatcher.TryPatch(harmony, "Pawn_FlightTracker.Notify_JobStarted (null job def fix)",
                AccessTools.Method(typeof(Pawn_FlightTracker), nameof(Pawn_FlightTracker.Notify_JobStarted)),
                prefix: new HarmonyMethod(typeof(VanilaBugFixPatch), nameof(Pawn_FlightTracker_Notify_JobStarted_Prefix)));
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

        private static bool Pawn_FlightTracker_Notify_JobStarted_Prefix(Job job, Pawn ___pawn)
        {
            if (job?.def == null) { return false; }
            return true;
        }
    }
}
