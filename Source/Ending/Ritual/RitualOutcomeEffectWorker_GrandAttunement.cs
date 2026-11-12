using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_GrandAttunement : RitualOutcomeEffectWorker_EndingEverflower
    {
        public const int GrandAttunementLevel = 5;
        private const float CameraShakeMagnitude = 1.5f;

        public RitualOutcomeEffectWorker_GrandAttunement() { }
        public RitualOutcomeEffectWorker_GrandAttunement(RitualOutcomeEffectDef def) : base(def) { }

        protected override void Apply(ArcanePlant_Everflower everflower, float quality, LordJob_Ritual jobRitual)
        {
            var map = everflower.Map;
            var compEverflower = everflower.EverflowerComp;

            var ending = ViviEndingQuest.Current;
            if (ending == null || !ending.Notify_GrandAttunementCompleted(everflower, quality))
            {
                everflower.Notify_RitualComplete(quality);
                return;
            }

            compEverflower.SetAttunementLevel(GrandAttunementLevel);

            def.effecter?.Spawn(everflower, map, compEverflower.RitualEffecterOffset).Cleanup();

            everflower.Notify_RitualComplete(quality);

            ending.Notify_EverflowerRitualEcho(everflower, jobRitual.PawnWithRole("resonator"));

            ApocryphaUtility.ShakeCamera(map, CameraShakeMagnitude);
        }
    }
}
