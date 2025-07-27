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
            }

            def.effecter.Spawn(everflower, map).Cleanup();

            everflower.Notify_RitualComplete(quality);
        }
    }
}
