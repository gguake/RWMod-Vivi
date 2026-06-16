using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_Defairyfication : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_Defairyfication() { }
        public RitualOutcomeEffectWorker_Defairyfication(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            var map = everflower.Map;

            var resonator = jobRitual.PawnWithRole("resonator");
            if (resonator != null && resonator.TryGetComp<CompViviHolder>(out var compViviHolder))
            {
                var fairyController = resonator.GetComp<CompViviFairyController>();
                if (fairyController != null && fairyController.MaterializedCount > 0)
                {
                    Messages.Message(LocalizeString_Etc.VV_FailReason_HasMaterializedFairyVivi.Translate(), resonator, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                var vivi = compViviHolder.DetachVivi();
                if (vivi != null)
                {
                    vivi.health.AddHediff(VVHediffDefOf.VV_FairyficationSickness);
                }
            }

            def.effecter.Spawn(everflower, map).Cleanup();

            everflower.Notify_RitualComplete(quality);
        }
    }
}
