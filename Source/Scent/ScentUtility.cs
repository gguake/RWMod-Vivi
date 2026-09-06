using Verse;

namespace VVRace
{
    public static class ScentUtility
    {
        public const int DiffusionIntervalTicks = 1000;

        // 향 hediff는 severity가 하루 -2.4로 감쇠하며, 0.2 간격 스테이지로 효과 단계가 결정된다.
        // 소스 severity가 0.2의 배수에 정확히 걸치면 감쇠·재적용 사이에 단계가 진동하므로
        // 경계에 걸치는 값은 피할 것 (방 확산이 0.2가 아니라 0.199인 이유).
        public const float FullScentSeverity = 1f;
        public const float GatherScentSeverity = 0.5f;
        public const float RoomScentSeverity = 0.199f;

        public static ScentExtension GetScentExtension(ThingDef def)
        {
            return def?.GetModExtension<ScentExtension>();
        }

        public static bool IsScentFlower(ThingDef def)
        {
            return GetScentExtension(def)?.scentHediff != null;
        }

        public static bool ApplyScent(Pawn pawn, ThingDef flowerDef, float severity = FullScentSeverity)
        {
            var ext = GetScentExtension(flowerDef);
            if (ext?.scentHediff == null) { return false; }
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.health == null) { return false; }
            if (pawn.RaceProps?.IsFlesh != true) { return false; }

            Hediff_FloralScent same = null;
            var maxOtherSeverity = 0f;
            var hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; ++i)
            {
                if (hediffs[i] is Hediff_FloralScent scent)
                {
                    if (scent.def == ext.scentHediff)
                    {
                        same = scent;
                    }
                    else if (scent.Severity > maxOtherSeverity)
                    {
                        maxOtherSeverity = scent.Severity;
                    }
                }
            }

            if (same != null)
            {
                if (same.Severity < severity)
                {
                    same.Severity = severity;
                }
                return true;
            }

            // 다른 향은 severity가 엄격히 높은 쪽만 우선한다. 동률이면 기존 향 유지.
            if (severity <= maxOtherSeverity) { return false; }

            for (int i = hediffs.Count - 1; i >= 0; --i)
            {
                if (hediffs[i] is Hediff_FloralScent scent)
                {
                    pawn.health.RemoveHediff(scent);
                }
            }

            // 건강 상태 판정 전에 농도를 설정해 최대 단계 효과가 일시적으로 적용되지 않게 한다.
            var added = HediffMaker.MakeHediff(ext.scentHediff, pawn);
            added.Severity = severity;
            pawn.health.AddHediff(added);

            return true;
        }

        public static void ApplyScentToRoom(Room room, ThingDef flowerDef)
        {
            foreach (var region in room.Regions)
            {
                foreach (var thing in region.ListerThings.ThingsInGroup(ThingRequestGroup.Pawn))
                {
                    if (thing is Pawn pawn)
                    {
                        ApplyScent(pawn, flowerDef, RoomScentSeverity);
                    }
                }
            }
        }
    }
}
