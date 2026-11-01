using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public abstract class RitualBehaviorWorker_EverflowerBase : RitualBehaviorWorker
    {
        protected RitualBehaviorWorker_EverflowerBase() { }
        protected RitualBehaviorWorker_EverflowerBase(RitualBehaviorDef def) : base(def) { }

        private Sustainer _soundPlaying;
        public override Sustainer SoundPlaying => _soundPlaying;

        protected virtual int LastSilentStageIndex => 0;

        public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null, Dictionary<string, Pawn> forcedForRole = null)
        {
            var everflower = target.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return "NotEverflower"; }

            return EverflowerRitualRules.GetBlockReason(everflower, ritual);
        }

        public override bool TargetStillAllowed(TargetInfo selectedTarget, LordJob_Ritual ritual)
        {
            if (!base.TargetStillAllowed(selectedTarget, ritual)) { return false; }

            return EverflowerRitualRules.CanContinueRitual(selectedTarget.Thing as ArcanePlant_Everflower, ritual.Ritual);
        }

        public override void Tick(LordJob_Ritual ritual)
        {
            if (ritual.StageIndex > LastSilentStageIndex)
            {
                if (_soundPlaying == null || _soundPlaying.Ended)
                {
                    _soundPlaying = VVSoundDefOf.VV_EverflowerRitualCast.TrySpawnSustainer(
                        SoundInfo.InMap(new TargetInfo(ritual.selectedTarget.Cell, ritual.selectedTarget.Map), MaintenanceType.PerTick));
                }

                _soundPlaying.Maintain();
            }
        }
    }
}
