using RimWorld;
using Verse;

namespace VVRace
{
    public static class EverflowerBlessingUtility
    {
        public const int RefreshIntervalTicks = 2000;

        public static void RefreshFor(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.health == null || !pawn.IsVivi()) { return; }

            var quest = ViviEndingQuest.Current;
            if (quest == null) { return; }

            var anchor = quest.AnchorEverflower;
            if (anchor == null || !anchor.Spawned || anchor.Map != pawn.Map) { return; }

            var compEverflower = anchor.EverflowerComp;
            var level = compEverflower.AttunementLevel;
            if (level < RitualOutcomeEffectWorker_GrandAttunement.GrandAttunementLevel) { return; }

            if (!pawn.IsRoyalVivi() && pawn.ageTracker.Adult)
            {
                MaintainReverberation(pawn, compEverflower.AttunementHediffSeverity);
            }

            if (quest.IsCompleted && level >= CompEverflower.MaxAttunementLevel && pawn.Faction != null && pawn.Faction.IsPlayer && pawn.ageTracker.Adult)
            {
                GiveOrRefreshBlessing(pawn);
            }
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
            var blessing = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerBlessing);
            if (blessing == null)
            {
                pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerBlessing);
                return;
            }

            blessing.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }
    }
}
