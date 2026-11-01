using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // EndingCorePatch가 이름으로 찾아 호출하므로 클래스명과 Patch 진입점을 유지한다.
    // 모드 생성자에서 실행되므로 Patch() 안에서는 DefOf나 DefDatabase를 읽지 않는다.
    internal static class EndingRitualPatch
    {
        internal static void Patch(Harmony harmony)
        {
            // 의식 취소만 막으면 공명자가 계속 도망쳐 의식이 끝나지 않으므로 역할 참가자의 도주도 막는다.
            var hostilityResponse = AccessTools.Method(typeof(JobGiver_ConfigurableHostilityResponse), "TryGiveJob");
            if (hostilityResponse == null)
            {
                Log.Error("[ViViRace] ending ritual patch: JobGiver_ConfigurableHostilityResponse.TryGiveJob not found");
            }
            else
            {
                harmony.Patch(hostilityResponse, prefix: new HarmonyMethod(typeof(EndingRitualPatch), nameof(ConfigurableHostilityResponse_TryGiveJob_Prefix)));
            }

            // 시작 대화창과 자동 배정이 이 정적 메서드를 공유하므로 해당 판정 중에만 출혈 무시를 임시로 켠다.
            var notAssignableReason = AccessTools.Method(typeof(RitualRoleAssignments), nameof(RitualRoleAssignments.PawnNotAssignableReason),
                new[] { typeof(Pawn), typeof(RitualRole), typeof(Precept_Ritual), typeof(RitualRoleAssignments), typeof(TargetInfo), typeof(bool).MakeByRefType() });
            if (notAssignableReason == null)
            {
                Log.Error("[ViViRace] ending ritual patch: RitualRoleAssignments.PawnNotAssignableReason not found");
            }
            else
            {
                harmony.Patch(notAssignableReason,
                    prefix: new HarmonyMethod(typeof(EndingRitualPatch), nameof(RitualRoleAssignments_PawnNotAssignableReason_Prefix)),
                    finalizer: new HarmonyMethod(typeof(EndingRitualPatch), nameof(RitualRoleAssignments_PawnNotAssignableReason_Finalizer)));
            }
        }

        // 보스 등장 후 관람자가 무방비로 남지 않도록 도주·반격은 기존 동작을 유지한다.
        private static bool ConfigurableHostilityResponse_TryGiveJob_Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn?.GetLord()?.LordJob is LordJob_RitualResonance ritual
                && EverflowerRitualRules.IsBossFightOnMap(pawn.MapHeld)
                && ritual.RoleFor(pawn, includeForced: true) != null)
            {
                __result = null;
                return false;
            }

            return true;
        }

        private static void RitualRoleAssignments_PawnNotAssignableReason_Prefix(RitualRole role, Precept_Ritual ritual, TargetInfo ritualTarget, out bool __state)
        {
            __state = EverflowerRitualRules.ShouldIgnoreBleeding(role, ritual, ritualTarget.Thing as ArcanePlant_Everflower);
            if (__state)
            {
                role.ignoreBleeding = true;
            }
        }

        private static Exception RitualRoleAssignments_PawnNotAssignableReason_Finalizer(Exception __exception, RitualRole role, bool __state)
        {
            if (__state)
            {
                role.ignoreBleeding = false;
            }

            return __exception;
        }
    }
}
