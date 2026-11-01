using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace VVRace
{
    // ChecksReservations는 대상을 받지 않아 앵커 의식만 구분할 수 없으므로 바닐라 검사를 유지한다.
    // 보스전 중 앵커의 예약 검사 예외는 LordJob_RitualResonance가 처리한다.
    public abstract class RitualBehaviorWorker_EverflowerBase : RitualBehaviorWorker
    {
        protected RitualBehaviorWorker_EverflowerBase() { }
        protected RitualBehaviorWorker_EverflowerBase(RitualBehaviorDef def) : base(def) { }

        private Sustainer _soundPlaying;
        public override Sustainer SoundPlaying => _soundPlaying;

        // 이 단계까지는 무음으로 진행하고 다음 단계부터 의식 소리를 재생한다.
        protected virtual int LastSilentStageIndex => 0;

        public override bool ShouldInitAsSpectator(Pawn pawn, RitualRoleAssignments assignments)
        {
            if (EverflowerRitualRules.IsBossFightOnMap(pawn.MapHeld)) { return false; }

            return base.ShouldInitAsSpectator(pawn, assignments);
        }

        protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual, RitualObligation obligation, RitualRoleAssignments assignments)
        {
            if (EverflowerRitualRules.IsAnchor(target.Thing as ArcanePlant_Everflower))
            {
                return new LordJob_RitualResonance(target, ritual, obligation, def.stages, assignments);
            }

            return base.CreateLordJob(target, organizer, ritual, obligation, assignments);
        }

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
