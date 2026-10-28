using HarmonyLib;
using RimWorld;
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
            // 보스전 중 앵커 영원꽃의 의식 참가자는 적이 가까이 있어도 '도주' 적대 반응으로 의식 자리를 떠나지 않는다.
            // (의식 취소만 막으면 공명자가 계속 도망쳐 의식이 끝나지 않는다. 바닐라 사이킥 의식 참가자 예외와 같은 방식)
            var target = AccessTools.Method(typeof(JobGiver_ConfigurableHostilityResponse), "TryGiveJob");
            if (target == null)
            {
                Log.Error("[ViViRace] ending ritual patch: JobGiver_ConfigurableHostilityResponse.TryGiveJob not found");
                return;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(typeof(EndingRitualPatch), nameof(ConfigurableHostilityResponse_TryGiveJob_Prefix)));
        }

        private static bool ConfigurableHostilityResponse_TryGiveJob_Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn?.GetLord()?.LordJob is LordJob_RitualResonance && EverflowerRitualRules.IsBossFightOnMap(pawn.MapHeld))
            {
                __result = null;
                return false;
            }

            return true;
        }
    }
}
