using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    // 멸망꽃 저주: 만개한 멸망꽃이 맵의 모든 정착민에게 무작위 페널티 1단계를 내리고,
    // 요정 간섭 비비가 결속을 해방하면 일괄 해제된다.
    public static class DoomBlightUtility
    {
        public const int MaxBlightLevel = 3;
        public const float ToxinPerBloom = 0.30f;

        private enum BlightKind
        {
            Hunger,
            Fatigue,
            Pain,
            Mind,
            Toxin,
        }

        // 저주 4종(단계형 헤디프). 중독 표식(VV_DoomBlight_Toxin)은 따로 처리한다.
        private static IEnumerable<HediffDef> StagedBlightDefs
        {
            get
            {
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Hunger;
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Fatigue;
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Pain;
                yield return ApocryphaGimmickDefOf.VV_DoomBlight_Mind;
            }
        }

        // 멸망꽃 만개: 5가지 페널티 중 하나를 골라 맵의 모든 스폰된 정착민에게 적용하고 메시지로 알린다.
        // 이미 모든 정착민이 최고 단계인 저주는 고르지 않는다(중독은 항상 후보).
        public static void ApplyDoomBlight(Map map, IntVec3 sourceCell)
        {
            if (map == null) { return; }

            var colonists = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            if (colonists.Count == 0) { return; }

            var candidates = new List<BlightKind>();
            foreach (BlightKind kind in System.Enum.GetValues(typeof(BlightKind)))
            {
                if (kind == BlightKind.Toxin || AnyBelowMaxLevel(colonists, GetBlightDef(kind)))
                {
                    candidates.Add(kind);
                }
            }

            var chosen = candidates.RandomElement();
            var lookTargets = new LookTargets(new TargetInfo(sourceCell, map));

            if (chosen == BlightKind.Toxin)
            {
                foreach (var pawn in colonists)
                {
                    AddToxin(pawn);
                }

                Messages.Message(
                    LocalizeString_EndingGimmicks.VV_Message_DoomFlowerBloomedToxin.Translate(),
                    lookTargets,
                    MessageTypeDefOf.NegativeEvent);
                return;
            }

            var blightDef = GetBlightDef(chosen);
            foreach (var pawn in colonists)
            {
                AddOrStackBlight(pawn, blightDef);
            }

            Messages.Message(
                LocalizeString_EndingGimmicks.VV_Message_DoomFlowerBloomed.Translate(blightDef.label.Named("BLIGHT")),
                lookTargets,
                MessageTypeDefOf.NegativeEvent);
        }

        // 결속 해방/엔딩 정리: 맵의 모든 폰에게서 저주 4종을 제거하고, 멸망꽃이 더했던 만큼 독성 축적을 되돌린다.
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
            var hediffSet = pawn.health.hediffSet;

            var toxinMarker = hediffSet.GetFirstHediffOfDef(ApocryphaGimmickDefOf.VV_DoomBlight_Toxin);
            if (toxinMarker != null)
            {
                // 그사이 자연적으로 감소했더라도 AdjustSeverity에서 수치가 0 아래로 내려가지 않도록 처리한다(0 이하가 되면 다음 틱에 제거).
                HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, -toxinMarker.Severity);
                pawn.health.RemoveHediff(toxinMarker);
                anyCleared = true;
            }

            foreach (var def in StagedBlightDefs)
            {
                var hediff = hediffSet.GetFirstHediffOfDef(def);
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                    anyCleared = true;
                }
            }

            return anyCleared;
        }

        private static HediffDef GetBlightDef(BlightKind kind)
        {
            switch (kind)
            {
                case BlightKind.Hunger: return ApocryphaGimmickDefOf.VV_DoomBlight_Hunger;
                case BlightKind.Fatigue: return ApocryphaGimmickDefOf.VV_DoomBlight_Fatigue;
                case BlightKind.Pain: return ApocryphaGimmickDefOf.VV_DoomBlight_Pain;
                case BlightKind.Mind: return ApocryphaGimmickDefOf.VV_DoomBlight_Mind;
                default: return ApocryphaGimmickDefOf.VV_DoomBlight_Toxin;
            }
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

        // 없으면 1단계로 추가, 있으면 한 단계 올린다(최대 3단계).
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

        // 독성 축적 +30%. 실제로 늘어난 양을 보이지 않는 표식 헤디프에 누적해 두었다가 해제 때 그만큼 되돌린다.
        private static void AddToxin(Pawn pawn)
        {
            if (pawn.Dead) { return; }

            var before = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup)?.Severity ?? 0f;
            HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, ToxinPerBloom);
            if (pawn.Dead) { return; }

            var after = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup)?.Severity ?? 0f;
            var added = after - before;
            if (added > 0f)
            {
                HealthUtility.AdjustSeverity(pawn, ApocryphaGimmickDefOf.VV_DoomBlight_Toxin, added);
            }
        }
    }
}
