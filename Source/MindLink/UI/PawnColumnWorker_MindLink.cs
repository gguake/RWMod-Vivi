using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class PawnColumnWorker_MindLink : PawnColumnWorker_Label
    {
        protected override TextAnchor LabelAlignment => TextAnchor.MiddleCenter;

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var linker = pawn.GetMindLinkMaster();
            if (linker != null)
            {
                base.DoCell(rect, linker, table);
            }
        }

        public override bool CanGroupWith(Pawn pawn, Pawn other)
        {
            var linker = pawn.GetMindLinkMaster();
            if (linker != null)
            {
                return other.GetMindLinkMaster() == linker;
            }
            return false;
        }
    }
}
