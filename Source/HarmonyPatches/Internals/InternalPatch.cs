using HarmonyLib;
using System;
using System.Reflection;
using Verse;

namespace VVRace.HarmonyPatches.Internals
{
    internal class ILOverrideAttribute : Attribute
    {
        public Type type;
        public string methodName;

        public ILOverrideAttribute(Type type, string methodName)
        {
            this.type = type;
            this.methodName = methodName;
        }
    }

    internal class InternalPatch
    {
        public static void Patch(Harmony harmony)
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (method.TryGetAttribute<ILOverrideAttribute>(out var attr))
                    {
                        var transpiler = AccessTools.Method(attr.type, attr.methodName);
                        harmony.Patch(method, transpiler: new HarmonyMethod(transpiler));
                    }
                }
            }
        }
    }
}
