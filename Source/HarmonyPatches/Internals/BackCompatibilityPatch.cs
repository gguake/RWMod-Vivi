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
        // 미완성품이 나중에 추가된 레시피 목록 (구버전 세이브의 Bill_Production -> Bill_ProductionWithUft)
        private static readonly HashSet<string> UftAddedRecipeDefNames = new HashSet<string>
        {
            "Make_VV_UniformHelmet",
        };

        // 클래스가 나중에 HediffWithComps에서 Hediff_ViviFairyFollow로 바뀐 헤디프
        private const string FairyBondHediffDefName = "VV_ViviFairyFollow";

        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(original: AccessTools.Method(typeof(BackCompatibility), nameof(BackCompatibility.GetBackCompatibleType)),
                postfix: new HarmonyMethod(typeof(BackCompatibilityPatch), nameof(BackCompatibility_GetBackCompatibleType_Postfix)));
        }

        private static void BackCompatibility_GetBackCompatibleType_Postfix(ref Type __result, XmlNode node)
        {
            if (__result == typeof(HediffWithComps))
            {
                // 구버전 세이브의 요정 비비 결속(HediffWithComps -> Hediff_ViviFairyFollow). 추가 저장 필드가 없어 그대로 읽힌다.
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
