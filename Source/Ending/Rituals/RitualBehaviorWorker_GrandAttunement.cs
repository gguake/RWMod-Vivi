using RimWorld;
using Verse;

namespace VVRace
{
    public class RitualBehaviorWorker_GrandAttunement : RitualBehaviorWorker_GeneralEverflowerRitual
    {
        private const int AssimilationStageIndex = 1;
        private const string ResonatorRoleId = "resonator";

        public RitualBehaviorWorker_GrandAttunement() { }
        public RitualBehaviorWorker_GrandAttunement(RitualBehaviorDef def) : base(def) { }

        public override void Tick(LordJob_Ritual ritual)
        {
            base.Tick(ritual);

            if (ritual.StageIndex < AssimilationStageIndex) { return; }

            var resonator = ritual.PawnWithRole(ResonatorRoleId);
            if (resonator == null || !resonator.Spawned || resonator.Dead || resonator.Downed) { return; }
            if (Hediff_EverflowerAssimilation.IsAssimilated(resonator)) { return; }

            ViviEndingQuest.Current?.BeginAssimilation(resonator, ritual.selectedTarget.Thing as ArcanePlant_Everflower);
        }
    }
}
