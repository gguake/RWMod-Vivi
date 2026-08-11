using Verse;

namespace VVRace
{
    public static class ScentUtility
    {
        public const int DiffusionIntervalTicks = 1000;

        // 향 hediff는 severity가 하루 -2.4로 감쇠
        public const float FullScentSeverity = 1f;
        public const float RoomScentSeverity = 0.1f;

        public static ScentExtension GetScentExtension(ThingDef def)
        {
            return def?.GetModExtension<ScentExtension>();
        }

        public static bool IsScentFlower(ThingDef def)
        {
            return GetScentExtension(def)?.scentHediff != null;
        }

        public static bool ApplyScent(Pawn pawn, ThingDef flowerDef, bool fromPerfume = false, float severity = FullScentSeverity)
        {
            var ext = GetScentExtension(flowerDef);
            if (ext?.scentHediff == null) { return false; }
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.health == null) { return false; }
            if (pawn.RaceProps?.IsFlesh != true) { return false; }

            Hediff_FloralScent same = null;
            var protectedByPerfume = false;
            var hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; ++i)
            {
                if (hediffs[i] is Hediff_FloralScent scent)
                {
                    if (scent.def == ext.scentHediff)
                    {
                        same = scent;
                    }
                    else if (scent.fromPerfume)
                    {
                        protectedByPerfume = true;
                    }
                }
            }

            if (same != null)
            {
                if (same.Severity < severity)
                {
                    same.Severity = severity;
                }
                if (fromPerfume) { same.fromPerfume = true; }
                return true;
            }

            if (!fromPerfume && protectedByPerfume) { return false; }

            for (int i = hediffs.Count - 1; i >= 0; --i)
            {
                if (hediffs[i] is Hediff_FloralScent scent)
                {
                    pawn.health.RemoveHediff(scent);
                }
            }

            if (pawn.health.AddHediff(ext.scentHediff) is Hediff_FloralScent added)
            {
                added.fromPerfume = fromPerfume;
                added.Severity = severity;
            }

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
                        ApplyScent(pawn, flowerDef, fromPerfume: false, severity: RoomScentSeverity);
                    }
                }
            }
        }
    }
}
