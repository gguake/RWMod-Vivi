using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_Attunement : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_Attunement() { }
        public RitualOutcomeEffectWorker_Attunement(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var attuner = jobRitual.PawnWithRole("resonator");
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            var map = everflower.Map;

            if (everflower != null)
            {
                everflower.EverflowerComp.LinkAttunement(attuner, quality);

                ApplyReverberation(attuner, everflower, map);
            }

            def.effecter.Spawn(everflower, map, everflower.EverflowerComp.RitualEffecterOffset).Cleanup();

            everflower.Notify_RitualComplete(quality);
        }

        private void ApplyReverberation(Pawn attuner, ArcanePlant_Everflower everflower, Map map)
        {
            if (attuner == null || map == null) { return; }

            var severity = everflower.EverflowerComp.AttunementHediffSeverity;
            if (severity <= 0f) { return; }

            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!pawn.IsVivi() || pawn.IsRoyalVivi()) { continue; }

                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_EverflowerReverberation);
                if (existing != null)
                {
                    pawn.health.RemoveHediff(existing);
                }

                var hediff = pawn.health.AddHediff(VVHediffDefOf.VV_EverflowerReverberation);
                hediff.Severity = severity;
            }
        }
    }
}
