using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class PawnColumnWorker_ViviWorkMode : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (!pawn.TryGetViviGene(out var vivi)) { return; }

            var workMode = vivi.ViviControlSettings?.AssignedWorkMode;
            if (workMode == null) { return; }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect rect2 = rect;
            rect2.xMin += 3f;
            Widgets.Label(rect2, workMode.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;

            if (Mouse.IsOver(rect))
            {
                var canControlLinked = pawn.GetMindLinkMaster().TryGetMindTransmitter(out var mindTransmitter) ? 
                    mindTransmitter.CanControlLinkedPawnsNow : 
                    (AcceptanceReport)false;

                TipSignal tooltip = pawn.GetTooltip();
                tooltip.text = "ClickToChangeWorkMode".Translate();
                if (canControlLinked == false && !canControlLinked.Reason.NullOrEmpty())
                {
                    ref string text = ref tooltip.text;
                    text = text + "\n\n" + ("DisabledCommand".Translate() + ": " + canControlLinked.Reason).Colorize(ColorLibrary.RedReadable);
                }

                TooltipHandler.TipRegion(rect, tooltip);
                if (canControlLinked == true && Widgets.ButtonInvisible(rect))
                {
                    Find.WindowStack.Add(new FloatMenu(MindLinkUtility.GetViviWorkModeFloatMenuOptions(pawn).ToList()));
                }

                Widgets.DrawHighlight(rect);
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), 160);
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
        }
    }
}
