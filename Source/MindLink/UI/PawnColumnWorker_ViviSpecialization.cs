using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class PawnColumnWorker_ViviSpecializationIcon : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (!pawn.TryGetViviGene(out var vivi)) { return; }

            var specializeDef = vivi.ViviMindLinkSettings?.AssignedSpecialization;

            Widgets.DrawTextureFitted(rect, specializeDef.uiIcon, 0.75f);
            TooltipHandler.TipRegion(rect, specializeDef.description);
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

    public class PawnColumnWorker_ViviSpecialization : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (!pawn.TryGetViviGene(out var vivi)) { return; }

            var specializeDef = vivi.ViviMindLinkSettings?.AssignedSpecialization;
            if (specializeDef == null) { return; }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect rect2 = rect;
            rect2.xMin += 3f;
            Widgets.Label(rect2, specializeDef.label);
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
                    Find.WindowStack.Add(new FloatMenu(MindLinkUtility.GetViviSpecializeFloatMenuOptions(pawn).ToList()));
                }

                Widgets.DrawHighlight(rect);
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), 80);
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return Mathf.Min(base.GetMaxWidth(table), GetMinWidth(table));
        }
    }
}
