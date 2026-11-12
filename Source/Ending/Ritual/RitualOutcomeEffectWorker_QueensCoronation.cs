using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_QueensCoronation : RitualOutcomeEffectWorker_EndingEverflower
    {
        public RitualOutcomeEffectWorker_QueensCoronation() { }
        public RitualOutcomeEffectWorker_QueensCoronation(RitualOutcomeEffectDef def) : base(def) { }

        protected override void Apply(ArcanePlant_Everflower everflower, float quality, LordJob_Ritual jobRitual)
        {
            var queen = jobRitual.PawnWithRole("resonator");
            if (queen != null && queen.Spawned)
            {
                def.effecter?.Spawn(queen, queen.Map).Cleanup();
            }

            if (queen != null && !queen.Dead)
            {
                ViviEndingUtility.GrantQueenPowers(queen, everflower);
            }

            everflower.Notify_RitualComplete(quality);
            ViviEndingQuest.Current?.Notify_CoronationCompleted(everflower, queen);
        }
    }
}
