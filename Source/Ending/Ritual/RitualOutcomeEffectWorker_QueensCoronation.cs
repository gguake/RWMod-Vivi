using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_QueensCoronation : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_QueensCoronation() { }
        public RitualOutcomeEffectWorker_QueensCoronation(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return; }

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
