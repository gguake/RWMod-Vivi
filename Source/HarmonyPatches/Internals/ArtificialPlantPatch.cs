using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
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

            //harmony.Patch(
            //    original: AccessTools.Method(typeof(GenConstruct), nameof(GenConstruct.CanPlaceBlueprintOver)),
            //    prefix: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(GenConstruct_CanPlaceBlueprintOver_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Alert_NeedBatteries), "NeedBatteries"),
                postfix: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(Alert_NeedBatteries_NeedBatteries_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(WeatherEvent_LightningStrike), nameof(WeatherEvent_LightningStrike.DoStrike)),
                transpiler: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(WeatherEvent_LightningStrike_DoStrike_Transpiler)));

            Log.Message("!! [ViViRace] plant patch complete");
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

        private static void GenConstruct_CanPlaceBlueprintOver_Postfix(ref bool __result, BuildableDef newDef, ThingDef oldDef)
        {
            if (!__result)
            {
                if (newDef is ThingDef newThingDef && typeof(ArtificialPlant).IsAssignableFrom(newThingDef.thingClass) && typeof(ArtificialPlantPot).IsAssignableFrom(oldDef.thingClass))
                {
                    __result = true;
                }
            }
        }

        private static void Alert_NeedBatteries_NeedBatteries_Postfix(ref bool __result, Map map)
        {
            if (__result && map.listerBuildings.ColonistsHaveBuilding(VVThingDefOf.VV_Thunderbud))
            {
                __result = false;
            }
        }

        private static Thing RelocateLightningStrikeLocation(ref IntVec3 loc, Map map)
        {
            if (loc.IsValid)
            {
                return null;
            }

            var candidates = map.listerBuildings.allBuildingsColonist.Where(v => v.TryGetComp<CompLightningLod>()?.Active ?? false).ToList();
            if (candidates.Count > 0)
            {
                return candidates.RandomElement();
            }

            return null;
        }

        private static IEnumerable<CodeInstruction> WeatherEvent_LightningStrike_DoStrike_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            var localRelocatedThingIndex = ilGenerator.DeclareLocal(typeof(Thing)).LocalIndex;
            var injectPart1Index = instructions.FirstIndexOfInstruction(
                OpCodes.Call, 
                AccessTools.Method(typeof(Verse.Sound.SoundStarter), nameof(Verse.Sound.SoundStarter.PlayOneShotOnCamera)));

            var injectionPart1 = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarga_S, 0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArtificialPlantPatch), nameof(RelocateLightningStrikeLocation))),
                new CodeInstruction(OpCodes.Stloc_S, localRelocatedThingIndex),
            };

            if (injectPart1Index < 0) { throw new InvalidOperationException($"failed to find injection point for WeatherEvent_LightningStrike_DoStrike_Transpiler.injectPart1Index"); }
            instructions.InsertRange(injectPart1Index + 1, injectionPart1);

            var conditionalSkipLabel = ilGenerator.DefineLabel();
            var explosionSkipLabel = ilGenerator.DefineLabel();
            var injectPart2Index = instructions.FirstIndexOfInstruction(
                OpCodes.Call, 
                AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Fogged), new Type[] { typeof(IntVec3), typeof(Map) }));

            var explosionIndex = instructions.FirstIndexOfInstruction(OpCodes.Call, AccessTools.Method(typeof(GenExplosion), nameof(GenExplosion.DoExplosion)));

            var injectionPart2 = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldloc_S, localRelocatedThingIndex),
                new CodeInstruction(OpCodes.Brfalse_S, conditionalSkipLabel),
                new CodeInstruction(OpCodes.Ldloc_S, localRelocatedThingIndex),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThingCompUtility), nameof(ThingCompUtility.TryGetComp)).MakeGenericMethod(new Type[] { typeof(CompLightningLod) })),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompLightningLod), nameof(CompLightningLod.OnThunderStrike))),
                new CodeInstruction(OpCodes.Br_S, explosionSkipLabel),
            };

            if (injectPart2Index < 0) { throw new InvalidOperationException($"failed to find injection point for WeatherEvent_LightningStrike_DoStrike_Transpiler.injectPart2Index"); }
            if (explosionIndex < 0) { throw new InvalidOperationException($"failed to find injection point for WeatherEvent_LightningStrike_DoStrike_Transpiler.explosionIndex"); }

            instructions[injectPart2Index + 2].labels.Add(conditionalSkipLabel);
            instructions[explosionIndex + 1].labels.Add(explosionSkipLabel);

            instructions.InsertRange(injectPart2Index + 2, injectionPart2);

            return instructions;
        }
    }
}
