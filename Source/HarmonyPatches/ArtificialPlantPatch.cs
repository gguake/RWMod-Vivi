using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace VVRace
{
    internal static class ArtificialPlantPatch
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(GenSpawn), nameof(GenSpawn.SpawningWipes)),
                postfix: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(GenSpawn_SpawningWipes_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Designator_Deconstruct), nameof(Designator_Deconstruct.CanDesignateThing)),
                prefix: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(Designator_Deconstruct_CanDesignateThing_Prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(InstallationDesignatorDatabase), "NewDesignatorFor"),
                prefix: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(InstallationDesignatorDatabase_NewDesignatorFor_Prefix)));
        }

        private static void GenSpawn_SpawningWipes_Postfix(ref bool __result, BuildableDef newEntDef, BuildableDef oldEntDef)
        {
            var newThingDef = newEntDef as ThingDef;
            var oldThingDef = oldEntDef as ThingDef;

            if (newThingDef == null || oldThingDef == null) { return; }
            if (typeof(ArtificialPlant).IsAssignableFrom(newThingDef.thingClass) && typeof(ArtificialPlantPot).IsAssignableFrom(oldThingDef.thingClass))
            {
                __result = false;
            }
        }

        private static bool Designator_Deconstruct_CanDesignateThing_Prefix(ref AcceptanceReport __result, Thing t)
        {
            if (t is ArtificialPlant || (t is MinifiedThing minified && minified.InnerThing is ArtificialPlant))
            {
                __result = false;
                return false;
            }

            return true;
        }

        private static bool InstallationDesignatorDatabase_NewDesignatorFor_Prefix(ref Designator_Install __result, ThingDef artDef)
        {
            if (artDef.thingClass == typeof(MinifiedArtificialPlant))
            {
                __result = new Designator_ReplantArtificialPlant();
                __result.hotKey = KeyBindingDefOf.Misc1;

                return false;
            }

            return true;
        }
    }
}
