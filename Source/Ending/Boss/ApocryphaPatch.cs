using HarmonyLib;
using RimWorld;
using Verse;

namespace VVRace
{
    // 아포크리파 본체 모듈의 Harmony 등록 지점. EndingCorePatch가 이름으로 찾아 호출한다.
    // 피해 흡수·사망 처리는 종족 컴프(CompApocrypha)와 전용 생각 트리로 해결한다.
    // 거대 말벌이 보스에게 적대하지 않도록 하고 호위 반경을 제한하는 처리는 MentalState_HornetBerserk·JobGiver_HornetBerserk를 직접 수정해 구현한다.
    internal static class ApocryphaPatch
    {
        internal static void Patch(Harmony harmony)
        {
            // 아포크리파는 기믹(결속 해방·공명 파동·마나 파동 의식)에 의한 기절 외의 모든 기절에 면역이다.
            // 피해를 거치지 않고 StunFor를 직접 부르는 무기·능력·다른 모드도 있으므로 StunFor 자체를 막는다.
            var stunFor = AccessTools.Method(typeof(StunHandler), nameof(StunHandler.StunFor));
            if (stunFor == null)
            {
                Log.Error("[ViViRace] Apocrypha patch: StunHandler.StunFor not found; Apocrypha is not immune to stuns.");
                return;
            }

            harmony.Patch(stunFor, prefix: new HarmonyMethod(typeof(ApocryphaPatch), nameof(StunHandler_StunFor_Prefix)));
        }

        private static bool StunHandler_StunFor_Prefix(StunHandler __instance, int ticks)
        {
            if (CompApocrypha.ApplyingGimmickStun) { return true; }
            if (!(__instance.parent is Pawn pawn) || !ApocryphaUtility.IsApocrypha(pawn)) { return true; }

            if (ticks > 0)
            {
                pawn.GetComp<CompApocrypha>()?.Notify_StunBlocked();
            }

            return false;
        }
    }
}
