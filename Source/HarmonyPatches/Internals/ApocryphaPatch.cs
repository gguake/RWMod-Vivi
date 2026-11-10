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
            // 무작위 시나리오도 모든 동물 종족을 시작 동물 후보에 넣으므로 최종 보스는 명시적으로 제외한다.
            ViviHarmonyPatcher.TryPatch(harmony, "ScenPart_StartingAnimal.PossibleAnimals (exclude Asphodel from starting animals)",
                AccessTools.Method(typeof(ScenPart_StartingAnimal), "PossibleAnimals"),
                postfix: new HarmonyMethod(typeof(ApocryphaPatch), nameof(ScenPart_StartingAnimal_PossibleAnimals_Postfix)));
        }

        private static void ScenPart_StartingAnimal_PossibleAnimals_Postfix(ref IEnumerable<PawnKindDef> __result)
        {
            __result = __result?.Where(v => v.race != VVThingDefOf.VV_Apocrypha);
        }
    }
}
