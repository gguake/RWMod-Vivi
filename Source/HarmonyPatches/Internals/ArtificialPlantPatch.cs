using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace VVRace
{
    internal static class ArtificialPlantPatch
    {
        internal static void Patch(Harmony harmony)
        {
            // Designator 관련
            harmony.Patch(
                original: AccessTools.Method(typeof(GenSpawn), nameof(GenSpawn.SpawningWipes)),
                postfix: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(GenSpawn_SpawningWipes_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(InstallationDesignatorDatabase), "NewDesignatorFor"),
                prefix: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(InstallationDesignatorDatabase_NewDesignatorFor_Prefix)));

            // 축전지 알람 끄기
            harmony.Patch(
                original: AccessTools.Method(typeof(Alert_NeedBatteries), "NeedBatteries"),
                postfix: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(Alert_NeedBatteries_NeedBatteries_Postfix)));

            // 번개 위치 재조정
            harmony.Patch(
                original: AccessTools.Method(typeof(WeatherEvent_LightningStrike), nameof(WeatherEvent_LightningStrike.FireEvent)),
                transpiler: new HarmonyMethod(typeof(ArtificialPlantPatch), nameof(WeatherEvent_LightningStrike_FireEvent_Transpiler)));

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

        private static void Alert_NeedBatteries_NeedBatteries_Postfix(ref bool __result, Map map)
        {
            if (__result && map.listerBuildings.ColonistsHaveBuilding(VVThingDefOf.VV_Thunderbud))
            {
                __result = false;
            }
        }

        private static IntVec3 RelocateLightningStrike(IntVec3 strikeLoc, Map map)
        {
            if (!strikeLoc.IsValid)
            {
                var candidates = map.listerBuildings.allBuildingsColonist.Where(v => v.TryGetComp<CompLightningLod>()?.Active ?? false).ToList();
                if (candidates.Count > 0)
                {
                    return candidates.RandomElement().Position;
                }
            }

            return strikeLoc;
        }

        private static IEnumerable<CodeInstruction> WeatherEvent_LightningStrike_FireEvent_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var instructions = codeInstructions.ToList();

            var injections = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(WeatherEvent_LightningStrike), "strikeLoc")),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(WeatherEvent), "map")),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArtificialPlantPatch), nameof(RelocateLightningStrike))),
                new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(WeatherEvent_LightningStrike), "strikeLoc")),
            };

            instructions.InsertRange(0, injections);
            return instructions;
        }

        private static CompLightningLod FindLightningBolt(Map map, IntVec3 loc)
        {
            if (!loc.IsValid) { return null; }

            var thing = loc.GetFirstThingWithComp<CompLightningLod>(map);
            return thing.TryGetComp<CompLightningLod>();
        }

        private static IEnumerable<CodeInstruction> WeatherEvent_LightningStrike_DoStrike_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator ilGenerator)
        {
            var instructions = codeInstructions.ToList();

            var localLightningBolt = ilGenerator.DeclareLocal(typeof(CompLightningLod));

            var doExplosionLabel = ilGenerator.DefineLabel();
            var skipExplosionLabel = ilGenerator.DefineLabel();

            var injectionIndex = instructions.FirstIndexOfInstruction(OpCodes.Call, operand: AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Fogged), new Type[] { typeof(IntVec3), typeof(Map) })) + 2;
            var injection = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArtificialPlantPatch), nameof(FindLightningBolt))),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Brfalse_S, doExplosionLabel),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompLightningLod), nameof(CompLightningLod.OnLightningStrike))),
                new CodeInstruction(OpCodes.Br_S, skipExplosionLabel),
                new CodeInstruction(OpCodes.Pop).WithLabels(doExplosionLabel),
            };
            instructions.InsertRange(injectionIndex, injection);

            var skipExplosionIndex = instructions.FirstIndexOfInstruction(OpCodes.Call, operand: AccessTools.Method(typeof(GenExplosion), nameof(GenExplosion.DoExplosion))) + 1;
            instructions[skipExplosionIndex].labels.Add(skipExplosionLabel);

            return instructions;
        }
    }
}
