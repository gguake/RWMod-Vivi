using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_Fairyfication : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_Fairyfication() { }
        public RitualOutcomeEffectWorker_Fairyfication(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            var map = everflower.Map;

            def.effecter.Spawn(everflower, map).Cleanup();

            var vivi = jobRitual.PawnWithRole("vivi");
            var resonator = jobRitual.PawnWithRole("resonator");
            if (resonator != null && resonator.TryGetComp<CompViviHolder>(out var compViviHolder) && vivi != null)
            {
                compViviHolder.JoinVivi(vivi);
            }

            everflower.Notify_RitualComplete(quality);
        }
    }
}
