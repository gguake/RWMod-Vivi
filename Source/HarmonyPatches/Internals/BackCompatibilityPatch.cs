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

        // 이름이 바뀐 세이브 저장 클래스: 세이브의 Class 속성(전체 타입명) -> 현재 타입
        // 구버전 세이브가 남아 있는 한 지우면 안 된다. 지우면 해당 노드가 통째로 버려진다.
        private static readonly Dictionary<string, Type> RenamedTypes = new Dictionary<string, Type>
        {
            { "VVRace.GameComponent_ViviPreceptMigration", typeof(GameComponent_BackCompatibilityMigration) },
        };

        internal static void Patch(Harmony harmony)
        {
            ViviHarmonyPatcher.TryPatch(harmony, "BackCompatibility.GetBackCompatibleType (legacy save types)",
                AccessTools.Method(typeof(BackCompatibility), nameof(BackCompatibility.GetBackCompatibleType)),
                postfix: new HarmonyMethod(typeof(BackCompatibilityPatch), nameof(BackCompatibility_GetBackCompatibleType_Postfix)));
        }

        private static void BackCompatibility_GetBackCompatibleType_Postfix(ref Type __result, Type baseType, string providedClassName, XmlNode node)
        {
            // 옛 이름은 어느 어셈블리에서도 찾을 수 없어 __result가 null이다. 아래의 타입별 조기 반환보다 먼저 처리해야 한다.
            if (__result == null && providedClassName != null
                && RenamedTypes.TryGetValue(providedClassName, out var renamedType)
                && (baseType == null || baseType.IsAssignableFrom(renamedType)))
            {
                __result = renamedType;
                return;
            }

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
