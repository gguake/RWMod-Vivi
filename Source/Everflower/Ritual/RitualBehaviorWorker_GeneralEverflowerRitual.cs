using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class RitualBehaviorWorker_GeneralEverflowerRitual : RitualBehaviorWorker_EverflowerBase
    {
        public RitualBehaviorWorker_GeneralEverflowerRitual() { }
        public RitualBehaviorWorker_GeneralEverflowerRitual(RitualBehaviorDef def) : base(def) { }

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
