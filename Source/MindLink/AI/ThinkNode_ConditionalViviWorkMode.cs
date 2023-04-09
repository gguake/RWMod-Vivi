using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ThinkNode_ConditionalViviWorkMode : ThinkNode_Conditional
    {
        public ViviWorkModeDef workMode;

        public override float GetPriority(Pawn pawn)
        {
            if (pawn.workSettings == null || !pawn.workSettings.EverWork)
            {
                return 0f;
            }

            if (!pawn.HasViviGene() || pawn.Faction != Faction.OfPlayer)
            {
                return 0f;
            }

            var timeAssignmentDef = ((pawn.timetable == null) ? TimeAssignmentDefOf.Anything : pawn.timetable.CurrentAssignment);
            if (timeAssignmentDef == TimeAssignmentDefOf.Anything)
            {
                return 5.5f;
            }
            if (timeAssignmentDef == TimeAssignmentDefOf.Work)
            {
                return 9f;
            }
            if (timeAssignmentDef == TimeAssignmentDefOf.Sleep)
            {
                return 3f;
            }
            if (timeAssignmentDef == TimeAssignmentDefOf.Joy)
            {
                return 2f;
            }
            if (timeAssignmentDef == TimeAssignmentDefOf.Meditate)
            {
                return 2f;
            }

            throw new NotImplementedException();
        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            ThinkNode_ConditionalViviWorkMode obj = (ThinkNode_ConditionalViviWorkMode)base.DeepCopy(resolve);
            obj.workMode = workMode;
            return obj;
        }

        protected override bool Satisfied(Pawn pawn)
        {
            if (!pawn.TryGetViviGene(out var vivi) || pawn.Faction != Faction.OfPlayer)
            {
                return false;
            }

            var assignedWorkMode = vivi.ViviControlSettings?.AssignedWorkMode;
            return workMode != null && workMode == assignedWorkMode;
        }
    }
}
