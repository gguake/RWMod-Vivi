using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class PawnColumnWorker_DraftVivi : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (!pawn.IsColonistPlayerControlled || !pawn.Spawned)
            {
                return;
            }

            var acceptanceReport = pawn.CanDraftVivi();
            if (acceptanceReport == true || !acceptanceReport.Reason.NullOrEmpty())
            {
                rect.xMin += (rect.width - 24f) / 2f;
                rect.yMin += (rect.height - 24f) / 2f;

                bool drafted = pawn.Drafted;
                Widgets.Checkbox(rect.position, ref drafted, 24f, paintable: def.paintable, disabled: !acceptanceReport);
                if (!acceptanceReport.Reason.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(rect, acceptanceReport.Reason.CapitalizeFirst());
                }
                if (drafted != pawn.Drafted)
                {
                    pawn.drafter.Drafted = drafted;
                }
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), 28);
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
        }

        public override int GetMinCellHeight(Pawn pawn)
        {
            return Mathf.Max(base.GetMinCellHeight(pawn), 24);
        }
    }
}
