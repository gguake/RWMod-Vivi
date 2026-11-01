using HarmonyLib;
using RimWorld;
using Verse;

namespace VVRace
{
    // 〈영원꽃 완전 동화〉(Hediff_EverflowerAssimilation) Harmony 패치. EndingCorePatch가 이름으로 찾아 호출한다.
    // 모두 빠른 등록부(Hediff_EverflowerAssimilation.IsAssimilated)만 보고, 동화 중이 아닌 폰에는 아무것도 바꾸지 않는다.
    internal static class EverflowerAssimilationPatch
    {
        internal static void Patch(Harmony harmony)
        {
            // 동면처럼 모든 욕구(음식·수면·오락·기분 등)가 줄지도 차지도 않는다.
            TryPatch(harmony, "Need.IsFrozen", AccessTools.PropertyGetter(typeof(Need), "IsFrozen"), nameof(Need_IsFrozen_Postfix));

            // 영원꽃에 고정되어 지시·징집·작업 대상이 되지 않는다(선택·정보 확인은 된다).
            TryPatch(harmony, "Pawn.IsColonistPlayerControlled", AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.IsColonistPlayerControlled)), nameof(Pawn_IsColonistPlayerControlled_Postfix));

            // 서 있는(떠 있는) 채로도 의사가 치료할 수 있다(바닐라는 침대에 누운 인간형만 치료한다).
            TryPatch(harmony, "WorkGiver_Tend.GoodLayingStatusForTend", AccessTools.Method(typeof(WorkGiver_Tend), nameof(WorkGiver_Tend.GoodLayingStatusForTend)), nameof(WorkGiver_Tend_GoodLayingStatusForTend_Postfix));

            // 다른 의식(2페이즈 공명 파동 등)의 역할·관람자로 배정되지 않는다(자리를 옮길 수 없어 의식이 진행되지 않으므로).
            TryPatch(harmony, "RitualRoleAssignments.PawnNotAssignableReason",
                AccessTools.Method(typeof(RitualRoleAssignments), nameof(RitualRoleAssignments.PawnNotAssignableReason),
                    new[] { typeof(Pawn), typeof(RitualRole), typeof(Precept_Ritual), typeof(RitualRoleAssignments), typeof(TargetInfo), typeof(bool).MakeByRefType() }),
                nameof(RitualRoleAssignments_PawnNotAssignableReason_Postfix));
        }

        private static void TryPatch(Harmony harmony, string label, System.Reflection.MethodBase original, string postfixName)
        {
            if (original == null)
            {
                Log.Error($"[ViViRace] everflower assimilation patch: failed to find {label}; this patch is skipped.");
                return;
            }

            harmony.Patch(original, postfix: new HarmonyMethod(typeof(EverflowerAssimilationPatch), postfixName));
        }

        private static void Need_IsFrozen_Postfix(Pawn ___pawn, ref bool __result)
        {
            if (!__result && Hediff_EverflowerAssimilation.IsAssimilated(___pawn))
            {
                __result = true;
            }
        }

        private static void Pawn_IsColonistPlayerControlled_Postfix(Pawn __instance, ref bool __result)
        {
            if (__result && Hediff_EverflowerAssimilation.IsAssimilated(__instance))
            {
                __result = false;
            }
        }

        private static void WorkGiver_Tend_GoodLayingStatusForTend_Postfix(Pawn patient, ref bool __result)
        {
            if (!__result && Hediff_EverflowerAssimilation.IsAssimilated(patient))
            {
                __result = true;
            }
        }

        private static void RitualRoleAssignments_PawnNotAssignableReason_Postfix(Pawn __0, ref string __result)
        {
            if (__result == null && Hediff_EverflowerAssimilation.IsAssimilated(__0))
            {
                __result = LocalizeString_EndingRituals.VV_FailReason_PawnAssimilated.Translate(__0.Named("PAWN")).Resolve();
            }
        }
    }
}
