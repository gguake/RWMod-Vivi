using RimWorld;
using System;
using Verse.AI;

namespace VVRace
{
    public static class ToilUtility
    {
        public static Toil WithInitAction(this Toil toil, Action action)
        {
            toil.initAction = action;
            return toil;
        }

        public static Toil WithTickAction(this Toil toil, Action action)
        {
            toil.tickAction = action;
            return toil;
        }

        public static Toil WithFinishAction(this Toil toil, Action action)
        {
            toil.AddFinishAction(action);
            return toil;
        }

        public static Toil WithFailCondition(this Toil toil, Func<bool> condition)
        {
            toil.AddFailCondition(condition);
            return toil;
        }

        public static Toil WithDefaultCompleteMode(this Toil toil, ToilCompleteMode mode)
        {
            toil.defaultCompleteMode = mode;
            return toil;
        }

        public static Toil WithActiveSkill(this Toil toil, Func<SkillDef> activeSkillGetter)
        {
            toil.activeSkill = activeSkillGetter;
            return toil;
        }

        public static Toil WithHandlingFacing(this Toil toil)
        {
            toil.handlingFacing = true;
            return toil;
        }
    }
}
