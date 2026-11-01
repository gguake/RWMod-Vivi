using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    internal static class ApocryphaPatch
    {
        internal static void Patch(Harmony harmony)
        {
            // 피해 없이 StunFor를 직접 호출하는 무기·능력·모드에도 기절 면역이 적용되도록 StunFor에서 차단한다.
            var stunFor = AccessTools.Method(typeof(StunHandler), nameof(StunHandler.StunFor));
            if (stunFor == null)
            {
                Log.Error("[ViViRace] Apocrypha patch: StunHandler.StunFor not found; Apocrypha is not immune to stuns.");
            }
            else
            {
                harmony.Patch(stunFor, prefix: new HarmonyMethod(typeof(ApocryphaPatch), nameof(StunHandler_StunFor_Prefix)));
            }

            // 무작위 시나리오도 모든 동물 종족을 시작 동물 후보에 넣으므로 최종 보스는 명시적으로 제외한다.
            var possibleAnimals = AccessTools.Method(typeof(ScenPart_StartingAnimal), "PossibleAnimals");
            if (possibleAnimals == null)
            {
                Log.Warning("[ViViRace] Apocrypha patch: ScenPart_StartingAnimal.PossibleAnimals not found; Apocrypha may be offered as a starting animal.");
            }
            else
            {
                harmony.Patch(possibleAnimals, postfix: new HarmonyMethod(typeof(ApocryphaPatch), nameof(ScenPart_StartingAnimal_PossibleAnimals_Postfix)));
            }
        }

        private static void ScenPart_StartingAnimal_PossibleAnimals_Postfix(ref IEnumerable<PawnKindDef> __result)
        {
            __result = __result?.Where(v => v.race != ApocryphaThingDefOf.VV_Apocrypha);
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
