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

        public override bool PawnCanFillRole(Pawn pawn, RitualRole role, out string reason, TargetInfo ritualTarget)
        {
            if (!base.PawnCanFillRole(pawn, role, out reason, ritualTarget)) { return false; }

            if (role == null && (pawn == null || !pawn.IsVivi()))
            {
                reason = LocalizeString_Etc.VV_FailReason_NotVivi.Translate();
                return false;
            }

            return true;
        }

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
