using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using VVRace.HarmonyPatches;

namespace VVRace
{
    internal static class ArcanePlantPatch
    {
        internal static void Patch(Harmony harmony)
        {
            // Designator 관련
            harmony.Patch(
                original: AccessTools.Method(typeof(GenSpawn), nameof(GenSpawn.SpawningWipes)),
                postfix: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(GenSpawn_SpawningWipes_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(InstallationDesignatorDatabase), "NewDesignatorFor"),
                prefix: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(InstallationDesignatorDatabase_NewDesignatorFor_Prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Designator_Build), nameof(Designator_Build.SelectedUpdate)),
                postfix: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(Designator_Build_SelectedUpdate_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(Designator_Install), nameof(Designator_Install.SelectedUpdate)),
                postfix: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(Designator_Install_SelectedUpdate_Postfix)));

            // 축전지 알람 끄기
            harmony.Patch(
                original: AccessTools.Method(typeof(Alert_NeedBatteries), "NeedBatteries"),
                postfix: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(Alert_NeedBatteries_NeedBatteries_Postfix)));

            // 번개 위치 재조정
            harmony.Patch(
                original: AccessTools.Method(typeof(WeatherEvent_LightningStrike), nameof(WeatherEvent_LightningStrike.FireEvent)),
                transpiler: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(WeatherEvent_LightningStrike_FireEvent_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(WeatherEvent_LightningStrike), nameof(WeatherEvent_LightningStrike.DoStrike)),
                transpiler: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(WeatherEvent_LightningStrike_DoStrike_Transpiler)));

            // 터렛 식물 관련
            harmony.Patch(
                original: AccessTools.Method(typeof(ThoughtWorker_Precept_HasAutomatedTurrets), nameof(ThoughtWorker_Precept_HasAutomatedTurrets.ResetStaticData)),
                postfix: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(ThoughtWorker_Precept_HasAutomatedTurrets_ResetStaticData_Postfix)));

            // 드랍포드 식물 관련
            harmony.Patch(
                original: AccessTools.Method(typeof(CompLaunchable), nameof(CompLaunchable.TryLaunch)),
                transpiler: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(CompLaunchable_TryLaunch_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(typeof(DropPodUtility), nameof(DropPodUtility.MakeDropPodAt)),
                transpiler: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(DropPodUtility_MakeDropPodAt_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method("RimWorld.WorkGiver_CleanFilth:HasJobOnThing"),
                postfix: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(WorkGiver_CleanFilth_HasJobOnThing_Postfix)));

            {
                var innerType = typeof(Map).GetNestedTypes(BindingFlags.NonPublic).FirstOrDefault(t => t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Any(m => m.Name.Contains("FinalizeLoading")));
                var innerMethod = innerType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(m => m.Name.Contains("FinalizeLoading"));
                harmony.Patch(innerMethod, postfix: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(Map_FinalizeLoading_OrderBy_Postfix)));
            }

            harmony.Patch(
                original: AccessTools.Method(typeof(Map), nameof(Map.MapPreTick)),
                postfix: new HarmonyMethod(typeof(ArcanePlantPatch), nameof(Map_MapPreTick_Postfix)));

            Log.Message("!! [ViViRace] arcane plant patch complete");
        }

        private static void GenSpawn_SpawningWipes_Postfix(ref bool __result, BuildableDef newEntDef, BuildableDef oldEntDef)
        {
            var newThingDef = newEntDef as ThingDef;
            var oldThingDef = oldEntDef as ThingDef;

            if (newThingDef == null || oldThingDef == null) { return; }
            if (typeof(ArcanePlant).IsAssignableFrom(newThingDef.thingClass) && typeof(ArcanePlantPot).IsAssignableFrom(oldThingDef.thingClass))
            {
                __result = false;
            }

            if (typeof(ArcanePlantPot).IsAssignableFrom(newThingDef.thingClass) && typeof(ArcanePlant).IsAssignableFrom(oldThingDef.thingClass))
            {
                __result = false;
            }

            if (typeof(ActiveTransporter).IsAssignableFrom(newThingDef.thingClass) && typeof(ActiveTransporter).IsAssignableFrom(oldThingDef.thingClass))
            {
                __result = false;
            }
        }

        private static bool InstallationDesignatorDatabase_NewDesignatorFor_Prefix(ref Designator_Install __result, ThingDef artDef)
        {
            if (artDef.thingClass == typeof(MinifiedArcanePlant))
            {
                __result = new Designator_ReplantArcanePlant();
                __result.hotKey = KeyBindingDefOf.Misc1;

                return false;
            }

            return true;
        }

        private static void Designator_Build_SelectedUpdate_Postfix(BuildableDef ___entDef)
        {
            if (___entDef is ThingDef thingDef && (typeof(ManaAcceptor).IsAssignableFrom(thingDef.thingClass) || typeof(ArcanePlantPot).IsAssignableFrom(thingDef.thingClass)))
            {
                SectionLayer_ThingsManaFluxGrid.DrawManaFluxGridOverlayThisFrame();
            }
        }

        private static void Designator_Install_SelectedUpdate_Postfix(Designator_Install __instance)
        {
            if (__instance.PlacingDef is ThingDef thingDef && (typeof(ManaAcceptor).IsAssignableFrom(thingDef.thingClass) || typeof(ArcanePlantPot).IsAssignableFrom(thingDef.thingClass)))
            {
                SectionLayer_ThingsManaFluxGrid.DrawManaFluxGridOverlayThisFrame();
            }
        }

        private static void Alert_NeedBatteries_NeedBatteries_Postfix(ref bool __result, Map map)
        {
            if (__result && map.listerBuildings.ColonistsHaveBuilding(VVThingDefOf.VV_Shockerbud))
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
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArcanePlantPatch), nameof(RelocateLightningStrike))),
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
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArcanePlantPatch), nameof(FindLightningBolt))),
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

        private static void ThoughtWorker_Precept_HasAutomatedTurrets_ResetStaticData_Postfix()
        {
            var field = AccessTools.Field(typeof(ThoughtWorker_Precept_HasAutomatedTurrets), "automatedTurretDefs");
            var list = field.GetValue(null) as List<ThingDef>;

            list.RemoveAll(v => typeof(ArcanePlant).IsAssignableFrom(v.thingClass));
        }


        private static IEnumerable<CodeInstruction> CompLaunchable_TryLaunch_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator il)
        {
            var instructions = codeInstructions.ToList();

            var index = instructions.FindIndex(v => v.opcode == OpCodes.Newobj && v.OperandIs(AccessTools.Constructor(typeof(ActiveTransporterInfo))));
            if (index < 0) { throw new NotImplementedException("failed to find index for patch CompLaunchable_TryLaunch"); }

            var skipLabel = il.DefineLabel();
            var jumpLabel = il.DefineLabel();
            instructions[index + 1].labels.Add(jumpLabel);

            var injections = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ThingComp), nameof(ThingComp.props))),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Isinst, typeof(CompProperties_LaunchableCustom)),
                new CodeInstruction(OpCodes.Brfalse_S, skipLabel),

                new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(ActiveDropPodInfoCustom), new Type[] { typeof(CompProperties_LaunchableCustom) })),
                new CodeInstruction(OpCodes.Br_S, jumpLabel),
                new CodeInstruction(OpCodes.Pop).WithLabels(skipLabel),
            };
            instructions.InsertRange(index, injections);

            return instructions;
        }

        private static IEnumerable<CodeInstruction> DropPodUtility_MakeDropPodAt_Transpiler(IEnumerable<CodeInstruction> codeInstructions, ILGenerator il)
        {
            var instructions = codeInstructions.ToList();

            // ActiveDropPod Patch
            {
                var index = instructions.FindIndex(
                    v =>
                    v.opcode == OpCodes.Call &&
                    v.OperandIs(AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing))));

                if (index < 0) { throw new NotImplementedException("failed to find index for patch DropPodUtility_MakeDropPodAt #1"); }

                var skipLabel = il.DefineLabel();
                var jumpLabel = il.DefineLabel();
                instructions[0].labels.Add(skipLabel);
                instructions[index - 1].labels.Add(jumpLabel);

                var injections = new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Isinst, typeof(ActiveDropPodInfoCustom)),
                    new CodeInstruction(OpCodes.Brfalse_S, skipLabel),

                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ActiveDropPodInfoCustom), nameof(ActiveDropPodInfoCustom.activeDropPod))),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Brtrue_S, jumpLabel),
                    new CodeInstruction(OpCodes.Pop),
                };
                instructions.InsertRange(0, injections);
            }

            // IncomingDropPod Patch
            {
                var injectionIndex = instructions.FindIndex(
                    v =>
                    v.opcode == OpCodes.Callvirt &&
                    v.OperandIs(AccessTools.PropertySetter(typeof(ActiveTransporter), nameof(ActiveTransporter.Contents))));

                var jumpIndex = instructions.FindIndex(
                    v =>
                    v.opcode == OpCodes.Call &&
                    v.OperandIs(AccessTools.Method(typeof(SkyfallerMaker), nameof(SkyfallerMaker.SpawnSkyfaller), new Type[] { typeof(ThingDef), typeof(Thing), typeof(IntVec3), typeof(Map) })));

                if (injectionIndex < 0 || jumpIndex < 0) { throw new NotImplementedException("failed to find index for patch DropPodUtility_MakeDropPodAt #2"); }

                var skipLabel = il.DefineLabel();
                var jumpLabel = il.DefineLabel();

                instructions[injectionIndex + 1].labels.Add(skipLabel);
                instructions[jumpIndex - 3].labels.Add(jumpLabel);

                var injections = new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Isinst, typeof(ActiveDropPodInfoCustom)),
                    new CodeInstruction(OpCodes.Brfalse_S, skipLabel),

                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ActiveDropPodInfoCustom), nameof(ActiveDropPodInfoCustom.incomingDropPod))),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Brtrue_S, jumpLabel),
                    new CodeInstruction(OpCodes.Pop),
                };

                instructions.InsertRange(injectionIndex + 1, injections);
            }

            return instructions;
        }

        private static void WorkGiver_CleanFilth_HasJobOnThing_Postfix(ref bool __result, Thing t, bool forced)
        {
            if (!__result || forced || t.def != VVThingDefOf.VV_FilthPollen) { return; }

            var room = t.GetRoom();
            if (room != null && room.ContainsThing(VVThingDefOf.VV_GatheringBarrel))
            {
                __result = false;
            }
        }

        private static void Map_FinalizeLoading_OrderBy_Postfix(ref float __result, Building t)
        {
            if (t is ArcanePlant)
            {
                __result += 0.00001f;
            }
        }

        private static void Map_MapPreTick_Postfix(Map __instance)
        {
            if (GenTicks.TicksGame % ManaGrid.DiffuseInterval == 0)
            {
                var grid = __instance.GetComponent<ManaGrid>();
                grid.MapComponentPreTick();
            }
        }
    }
}
