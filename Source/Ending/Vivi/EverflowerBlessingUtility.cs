using RimWorld;
using Verse;

namespace VVRace
{
    public static class EverflowerBlessingUtility
    {
        public const int RefreshIntervalTicks = 2000;

        private const int ReverberationAttunementLevel = 5;
        private const int BlessingAttunementLevel = 6;

        public static bool IsBlessed(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) { return false; }

            if (pawn.health.hediffSet.HasHediff(EndingRitualDefOf.VV_EverflowerBlessing)) { return true; }

            // 아이 비비는 성장이 멈추지 않도록 노화 감속 헤디프를 제외하되, 축복받은 땅에서는 유전자 불안정 면역을 적용한다.
            return pawn.Faction != null && pawn.Faction.IsPlayer && pawn.IsVivi() && IsBlessedMap(pawn.MapHeld);
        }

        public static bool IsBlessedMap(Map map)
        {
            var quest = ViviEndingQuest.Current;
            if (quest == null || !quest.IsCompleted) { return false; }

            var anchor = GetSpawnedAnchor(quest, map);
            return anchor != null && anchor.AttunementLevel >= BlessingAttunementLevel;
        }

        public static void RefreshFor(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.health == null || !pawn.IsVivi()) { return; }

            var quest = ViviEndingQuest.Current;
            if (quest == null) { return; }

            var anchor = GetSpawnedAnchor(quest, pawn.Map);
            if (anchor == null) { return; }

            var compEverflower = anchor.EverflowerComp;
            var level = compEverflower.AttunementLevel;
            if (level < ReverberationAttunementLevel) { return; }

            if (!pawn.IsRoyalVivi() && pawn.ageTracker.Adult)
            {
                MaintainReverberation(pawn, compEverflower.AttunementHediffSeverity);
            }

            if (quest.IsCompleted && level >= BlessingAttunementLevel && pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                if (pawn.ageTracker.Adult)
                {
                    GiveOrRefreshBlessing(pawn);
                }

                RemoveGeneticUnstability(pawn);
            }
        }

        private static ArcanePlant_Everflower GetSpawnedAnchor(ViviEndingQuest quest, Map map)
        {
            var anchor = quest.AnchorEverflower;
            if (map == null || anchor == null || !anchor.Spawned || anchor.Map != map) { return null; }

            return anchor;
        }

        private static void MaintainReverberation(Pawn pawn, float severity)
        {
            if (severity <= 0f) { return; }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerReverberation)
                ?? pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerReverberation);

            if (hediff != null && hediff.Severity < severity)
            {
                hediff.Severity = severity;
            }
        }

        private static void GiveOrRefreshBlessing(Pawn pawn)
        {
            var blessing = pawn.health.hediffSet.GetFirstHediffOfDef(EndingRitualDefOf.VV_EverflowerBlessing);
            if (blessing == null)
            {
                pawn.health.AddHediff(EndingRitualDefOf.VV_EverflowerBlessing);
                return;
            }

            blessing.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }

        private static void RemoveGeneticUnstability(Pawn pawn)
        {
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(EndingRitualDefOf.VV_GeneticUnstability);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}
