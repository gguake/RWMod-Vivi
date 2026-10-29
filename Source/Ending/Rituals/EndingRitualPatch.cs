using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 엔딩 퀘스트 — 의식·비비 보상(Task B) Harmony 패치. EndingCorePatch.Patch가 이름으로 찾아 호출한다.
    // 패치를 추가할 때는 Patch() 안에서 DefOf/DefDatabase를 읽지 말 것(모드 생성자 시점).
    internal static class EndingRitualPatch
    {
        internal static void Patch(Harmony harmony)
        {
            // 보스전 중 앵커 영원꽃 의식의 역할 참가자는 적이 가까이 있어도 '도주' 적대 반응으로 의식 자리를 떠나지 않는다.
            // (의식 취소만 막으면 공명자가 계속 도망쳐 의식이 끝나지 않는다. 바닐라 사이킥 의식 참가자 예외와 같은 방식)
            var hostilityResponse = AccessTools.Method(typeof(JobGiver_ConfigurableHostilityResponse), "TryGiveJob");
            if (hostilityResponse == null)
            {
                Log.Error("[ViViRace] ending ritual patch: JobGiver_ConfigurableHostilityResponse.TryGiveJob not found");
            }
            else
            {
                harmony.Patch(hostilityResponse, prefix: new HarmonyMethod(typeof(EndingRitualPatch), nameof(ConfigurableHostilityResponse_TryGiveJob_Prefix)));
            }

            // 2페이즈 공명 파동(앵커 영원꽃의 조율 의식)에는 출혈 중인 공명자도 배정할 수 있다.
            // 시작 대화창·자동 배정의 검사가 모두 이 정적 메서드를 지나므로, 판정 동안만 역할의 출혈 무시를 켠다.
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

        // 관람자는 평소처럼 도주·반격한다(평화 기간에 시작한 의식의 관람자가 보스 등장 뒤 무방비로 남지 않게 한다).
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
