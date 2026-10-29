using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace VVRace
{
    // 영원꽃 의식 행동 워커(조율/일반/요정화)의 공통 부분. 엔딩 규칙(EverflowerRitualRules)과 의식 소리를 한 곳에서 처리하고,
    // 하위 워커는 역할 조건(PawnCanFillRole)과 소리를 내기 시작하는 단계만 정한다.
    // 예약 검사(ChecksReservations)는 바닐라 그대로 둔다. 대상을 받지 않아 앵커로 범위를 좁힐 수 없으므로,
    // 보스전 중 앵커 영원꽃의 의식만 앵커 로드(LordJob_RitualResonance)가 예약 검사를 건너뛴다.
    public abstract class RitualBehaviorWorker_EverflowerBase : RitualBehaviorWorker
    {
        protected RitualBehaviorWorker_EverflowerBase() { }
        protected RitualBehaviorWorker_EverflowerBase(RitualBehaviorDef def) : base(def) { }

        private Sustainer _soundPlaying;
        public override Sustainer SoundPlaying => _soundPlaying;

        // 소리 없이 진행하는 마지막 단계 번호. 이 단계 뒤부터 의식 소리를 낸다.
        protected virtual int LastSilentStageIndex => 0;

        // 보스전 중에는 엔딩 맵의 방어 인원을 관람자로 모집하지 않는다.
        public override bool ShouldInitAsSpectator(Pawn pawn, RitualRoleAssignments assignments)
        {
            if (EverflowerRitualRules.IsBossFightOnMap(pawn.MapHeld)) { return false; }

            return base.ShouldInitAsSpectator(pawn, assignments);
        }

        // 앵커 영원꽃의 의식은 보스전 동안 적이 다가와도 취소되지 않는 로드로 진행한다(보스전 여부는 로드가 매번 확인).
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

        // 아포크리파 2페이즈가 시작되면 앵커 영원꽃에서 진행 중이던 조율 외 의식은 간섭으로 취소된다.
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
