using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public abstract class RitualOutcomeEffectWorker_EndingEverflower : RitualOutcomeEffectWorker_FromQuality
    {
        protected RitualOutcomeEffectWorker_EndingEverflower() { }
        protected RitualOutcomeEffectWorker_EndingEverflower(RitualOutcomeEffectDef def) : base(def) { }

        public sealed override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return; }

            Apply(everflower, quality, jobRitual);
        }

        protected abstract void Apply(ArcanePlant_Everflower everflower, float quality, LordJob_Ritual jobRitual);
    }
}
