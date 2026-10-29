using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    // 멸망꽃 저주: 만개한 멸망꽃이 맵의 모든 정착민에게 무작위 저주(공포·허기·역병 중 하나)를 1단계 내리고,
    // 요정 간섭 비비가 결속을 해방하면 일괄 해제된다.
    public static class DoomBlightUtility
    {
        public const int MaxBlightLevel = 5;

        // 저주 3종(5단계 헤디프): 공포(고통 한계·정신 붕괴 기준), 허기(허기·피로), 역병(면역 획득·혈액 손실 회복·받는 피해)
        private static IEnumerable<HediffDef> BlightDefs
        {
            get
            {
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Fear;
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Hunger;
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Plague;
            }
        }

        // 멸망꽃 만개: 저주 3종 중 하나를 골라 맵의 모든 스폰된 정착민에게 1단계 더하고 메시지로 알린다.
        // 이미 모든 정착민이 최고 단계인 저주는 고르지 않는다.
        public static void ApplyDoomBlight(Map map, IntVec3 sourceCell)
        {
            if (map == null) { return; }

            var colonists = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            if (colonists.Count == 0) { return; }

            // 모든 저주가 이미 한계라 더 내릴 것이 없으면 시들기만 한다.
            if (!BlightDefs.Where(def => AnyBelowMaxLevel(colonists, def)).TryRandomElement(out var blightDef)) { return; }

            foreach (var pawn in colonists)
            {
                AddOrStackBlight(pawn, blightDef);
            }

            Messages.Message(
                LocalizeString_EndingGimmicks.VV_Message_DoomFlowerBloomed.Translate(blightDef.label.Named("BLIGHT")),
                new LookTargets(new TargetInfo(sourceCell, map)),
                MessageTypeDefOf.NegativeEvent);
        }

        // 결속 해방/엔딩 정리: 맵의 모든 폰에게서 저주 3종을 제거한다.
        public static bool ClearDoomBlights(Map map)
        {
            if (map == null) { return false; }

            var anyCleared = false;
            var pawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < pawns.Count; i++)
            {
                anyCleared |= ClearDoomBlights(pawns[i]);
            }

            return anyCleared;
        }

        public static bool ClearDoomBlights(Pawn pawn)
        {
            if (pawn?.health == null) { return false; }

            var anyCleared = false;
            foreach (var def in BlightDefs)
            {
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                    anyCleared = true;
                }
            }

            return anyCleared;
        }

        private static bool AnyBelowMaxLevel(List<Pawn> pawns, HediffDef def)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                var hediff = pawns[i].health.hediffSet.GetFirstHediffOfDef(def);
                if (hediff == null || hediff.Severity < MaxBlightLevel) { return true; }
            }

            return false;
        }

        // 없으면 1단계로 추가, 있으면 한 단계 올린다(최대 MaxBlightLevel단계).
        private static void AddOrStackBlight(Pawn pawn, HediffDef def)
        {
            if (pawn.Dead) { return; }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(def, pawn);
                hediff.Severity = 1f;
                pawn.health.AddHediff(hediff);
            }
            else
            {
                hediff.Severity = System.Math.Min(MaxBlightLevel, hediff.Severity + 1f);
            }
        }
    }
}
