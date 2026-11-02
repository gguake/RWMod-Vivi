using RimWorld;
using Verse;

namespace VVRace
{
    public class RitualBehaviorWorker_Fairyfication : RitualBehaviorWorker_GeneralEverflowerRitual
    {
        public RitualBehaviorWorker_Fairyfication() { }
        public RitualBehaviorWorker_Fairyfication(RitualBehaviorDef def) : base(def) { }

        protected override int LastSilentStageIndex => 1;

        public override bool PawnCanFillRole(Pawn pawn, RitualRole role, out string reason, TargetInfo ritualTarget)
        {
            if (!base.PawnCanFillRole(pawn, role, out reason, ritualTarget)) { return false; }

            if (role is RitualRoleEverflowerResonator)
            {
                if (!pawn.TryGetComp<CompViviHolder>(out var compViviHolder))
                {
                    return false;
                }

                if (!compViviHolder.CanJoin)
                {
                    reason = LocalizeString_Etc.VV_FailReason_FairyViviFull.Translate();
                    return false;
                }
            }

            return true;
        }
    }
}
