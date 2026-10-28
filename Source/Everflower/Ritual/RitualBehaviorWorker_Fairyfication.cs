using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace VVRace
{
    public class RitualBehaviorWorker_Fairyfication : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_Fairyfication() { }
        public RitualBehaviorWorker_Fairyfication(RitualBehaviorDef def) : base(def) { }

        private Sustainer _soundPlaying;
        public override Sustainer SoundPlaying => _soundPlaying;

        // 보스전 중에는 다른 폰(채집자 등)이 영원꽃을 예약해도 의식을 취소하지 않는다.
        public override bool ChecksReservations => !EverflowerRitualRules.IsBossFightActive;

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

        // 아포크리파 2페이즈가 시작되면 앵커 영원꽃에서 진행 중이던 요정화 의식은 간섭으로 취소된다.
        public override bool TargetStillAllowed(TargetInfo selectedTarget, LordJob_Ritual ritual)
        {
            if (!base.TargetStillAllowed(selectedTarget, ritual)) { return false; }

            return EverflowerRitualRules.CanContinueRitual(selectedTarget.Thing as ArcanePlant_Everflower, ritual.Ritual);
        }

        public override bool PawnCanFillRole(Pawn pawn, RitualRole role, out string reason, TargetInfo ritualTarget)
        {
            reason = null;

            if (role is RitualRoleEverflowerResonator)
            {
                var everflower = ritualTarget.Thing as ArcanePlant_Everflower;
                if (everflower == null) { return false; }

                if (!pawn.TryGetComp<CompVivi>(out var compVivi) || !compVivi.isRoyal)
                {
                    reason = LocalizeString_Etc.VV_FailReason_NotRoyalVivi.Translate();
                    return false;
                }

                if (compVivi.LinkedEverflower != everflower)
                {
                    reason = LocalizeString_Etc.VV_FailReason_NotAttunedEverflower.Translate();
                    return false;
                }

                if (!pawn.TryGetComp<CompViviHolder>(out var compViviHolder))
                {
                    return false;
                }

                if (!compViviHolder.CanJoin)
                {
                    reason = LocalizeString_Etc.VV_FailReason_FairyViviFull.Translate();
                    return false;
                }

                if (!pawn.CanReach(everflower, PathEndMode.Touch, Danger.Deadly))
                {
                    reason = "NoPath".Translate();
                    return false;
                }
            }

            return true;
        }

        public override void Tick(LordJob_Ritual ritual)
        {
            if (ritual.StageIndex > 1)
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
