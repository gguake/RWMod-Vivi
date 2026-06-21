using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VVRace
{
    public class RitualBehaviorWorker_Attunement : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_Attunement() { }
        public RitualBehaviorWorker_Attunement(RitualBehaviorDef def) : base(def) { }

        private Sustainer _soundPlaying;
        public override Sustainer SoundPlaying => _soundPlaying;

        public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null, Dictionary<string, Pawn> forcedForRole = null)
        {
            var everflower = target.Thing as ArcanePlant_Everflower;
            if (everflower == null) { return "NotEverflower"; }

            if (everflower.HasRitualCooldown)
            {
                var cooldownTicksRemaining = everflower.CurRitualCooldownTicks - GenTicks.TicksGame;
                return LocalizeString_Etc.VV_FailReason_RitualCooldown.Translate(cooldownTicksRemaining.ToStringTicksToPeriod());
            }

            return null;
        }

        public override bool PawnCanFillRole(Pawn pawn, RitualRole role, out string reason, TargetInfo ritualTarget)
        {
            reason = null;

            if (role is RitualRoleEverflowerAttuner)
            {
                var everflower = ritualTarget.Thing as ArcanePlant_Everflower;
                if (everflower == null) { return false; }

                if (!pawn.TryGetComp<CompVivi>(out var compVivi) || !compVivi.isRoyal)
                {
                    reason = LocalizeString_Etc.VV_FailReason_NotRoyalVivi.Translate();
                    return false;
                }

                if (compVivi.LinkedEverflower != null && compVivi.LinkedEverflower != everflower)
                {
                    reason = LocalizeString_Etc.VV_FailReason_AlreadyAttunedOther.Translate();
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
            if (ritual.StageIndex > 0)
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
