using Verse;

namespace VVRace
{
    public static class ScentUtility
    {
        public const int DiffusionIntervalTicks = 1000;

        // 향 헤디프의 severity는 하루에 2.4씩 줄고 효과 단계는 0.2 간격으로 바뀐다.
        // 0.2의 배수에서는 감쇠와 재적용으로 단계가 오르내리므로 확산 농도를 0.199로 둔다.
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
