using Verse;

namespace VVRace
{
    public static class ScentUtility
    {
        public static ScentExtension GetScentExtension(ThingDef def)
        {
            return def?.GetModExtension<ScentExtension>();
        }

        public static bool IsScentFlower(ThingDef def)
        {
            return GetScentExtension(def)?.scentHediff != null;
        }

        public static bool ApplyScent(Pawn pawn, ThingDef flowerDef, bool fromPerfume = false)
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
                same.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                if (fromPerfume) { same.fromPerfume = true; }
                return true;
            }

            // 향수로 부여된 다른 향은 향수 분사로만 교체할 수 있다.
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
            }

            return true;
        }
    }
}
