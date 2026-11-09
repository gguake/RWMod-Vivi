using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace VVRace
{
    internal class BackCompatibilityPatch
    {
        // 미완성품 지원이 뒤늦게 추가되어 구버전 Bill_Production을 Bill_ProductionWithUft로 변환해야 하는 레시피.
        private static readonly HashSet<string> UftAddedRecipeDefNames = new HashSet<string>
        {
            "Make_VV_UniformHelmet",
        };

        private const string FairyBondHediffDefName = "VV_ViviFairyFollow";

        internal static void Patch(Harmony harmony)
        {
            ViviHarmonyPatcher.TryPatch(harmony, "BackCompatibility.GetBackCompatibleType (legacy save types)",
                AccessTools.Method(typeof(BackCompatibility), nameof(BackCompatibility.GetBackCompatibleType)),
                postfix: new HarmonyMethod(typeof(BackCompatibilityPatch), nameof(BackCompatibility_GetBackCompatibleType_Postfix)));
        }

        private static void BackCompatibility_GetBackCompatibleType_Postfix(ref Type __result, XmlNode node)
        {
            if (__result == typeof(HediffWithComps))
            {
                // 추가 저장 필드가 없어 구버전 HediffWithComps 데이터를 Hediff_ViviFairyFollow로 그대로 읽을 수 있다.
                if (node?["def"]?.InnerText == FairyBondHediffDefName)
                {
                    __result = typeof(Hediff_ViviFairyFollow);
                }

                return;
            }

            if (__result != typeof(Bill_Production)) { return; }

            var recipeName = node?["recipe"]?.InnerText;
            if (recipeName == null || !UftAddedRecipeDefNames.Contains(recipeName)) { return; }

            __result = typeof(Bill_ProductionWithUft);
        }
    }
}
