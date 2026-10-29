using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 멸망꽃 저주: 만개한 멸망꽃이 맵의 모든 정착민에게 무작위 페널티 1단계를 내리고,
    // 요정 간섭 비비가 결속을 해방하면 일괄 해제된다.
    public static class DoomBlightUtility
    {
        public const int MaxBlightLevel = 3;
        public const float ToxinPerBloom = 0.30f;

        // 멸망꽃 중독이 올릴 수 있는 독성 축적 상한(바닐라 '심각' 단계 시작값).
        // 그 위 '극심'(0.8)은 의식이 10%로 묶여 쓰러지고 1.0이면 죽으므로, 결속 해방으로 풀리는 페널티가 정착민을 쓰러뜨리거나 죽이지 않게 여기서 멈춘다.
        public const float MaxToxicBuildupFromBlight = 0.60f;

        // 이보다 적게 더할 수 있으면(면역에 가깝거나 이미 상한) 중독 저주를 더 받을 수 없는 것으로 본다.
        private const float MinToxinToAdd = 0.01f;

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
        // 이미 모든 정착민이 최고 단계인 저주는 고르지 않는다(중독은 모든 정착민이 상한이거나 면역일 때 최고 단계로 본다).
        public static void ApplyDoomBlight(Map map, IntVec3 sourceCell)
        {
            if (map == null) { return; }

            var colonists = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            if (colonists.Count == 0) { return; }

            var candidates = new List<BlightKind>();
            foreach (BlightKind kind in System.Enum.GetValues(typeof(BlightKind)))
            {
                var available = kind == BlightKind.Toxin ? AnyCanTakeToxin(colonists) : AnyBelowMaxLevel(colonists, GetBlightDef(kind));
                if (available)
                {
                    candidates.Add(kind);
                }
            }

            // 모든 저주가 이미 한계라 더 내릴 것이 없으면 시들기만 한다.
            if (candidates.Count == 0) { return; }

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

        // 결속 해방/엔딩 정리: 맵의 모든 폰에게서 저주 4종과 중독 표식을 제거한다(표식이 제거되며 멸망꽃이 더했던 만큼 독성 축적을 되돌린다).
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

            // 독성 축적은 표식이 제거될 때(Hediff_DoomBlight.PostRemoved) 되돌린다.
            var toxinMarker = hediffSet.GetFirstHediffOfDef(ApocryphaGimmickDefOf.VV_DoomBlight_Toxin);
            if (toxinMarker != null)
            {
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

        // 중독 표식이 제거될 때(결속 해방·엔딩 정리·엔딩 종료 뒤 자동 제거·사망) 멸망꽃이 더했던 만큼 독성 축적을 되돌린다.
        // 그사이 자연적으로 감소했더라도 AdjustSeverity에서 수치가 0 아래로 내려가지 않도록 처리한다(0 이하가 되면 다음 틱에 제거).
        internal static void RevertToxin(Pawn pawn, float amount)
        {
            if (pawn?.health == null || amount <= 0f) { return; }

            HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, -amount);
        }

        private static bool AnyCanTakeToxin(List<Pawn> pawns)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                if (GetToxinToAdd(pawns[i]) >= MinToxinToAdd) { return true; }
            }

            return false;
        }

        // 이번 만개로 더할 독성 축적량: +30%를 독성 저항만큼 줄이고, 독성 축적이 MaxToxicBuildupFromBlight를 넘지 않게 자른다.
        private static float GetToxinToAdd(Pawn pawn)
        {
            if (pawn.Dead) { return 0f; }

            var amount = ToxinPerBloom * Mathf.Max(0f, 1f - pawn.GetStatValue(StatDefOf.ToxicResistance));
            var current = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup)?.Severity ?? 0f;
            return Mathf.Max(0f, Mathf.Min(amount, MaxToxicBuildupFromBlight - current));
        }

        // 독성 축적 +30%(독성 저항 반영, 상한까지). 실제로 늘어난 양을 보이지 않는 표식 헤디프에 누적해 두었다가 표식이 사라질 때 그만큼 되돌린다.
        private static void AddToxin(Pawn pawn)
        {
            var amount = GetToxinToAdd(pawn);
            if (amount < MinToxinToAdd) { return; }

            var before = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup)?.Severity ?? 0f;
            HealthUtility.AdjustSeverity(pawn, HediffDefOf.ToxicBuildup, amount);
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
