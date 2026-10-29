using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class RitualBehaviorWorker_Fairyfication : RitualBehaviorWorker_EverflowerBase
    {
        public RitualBehaviorWorker_Fairyfication() { }
        public RitualBehaviorWorker_Fairyfication(RitualBehaviorDef def) : base(def) { }

        // 요정화 의식은 대상 비비를 데려와 눕히고 공명자가 자리를 잡을 때까지(0~1단계) 소리 없이 진행한다.
        protected override int LastSilentStageIndex => 1;

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
    }
}
