using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class MindTransmitterBandwidthGizmo : Gizmo
    {
        private const float Width = 136f;
        private const int HeaderHeight = 20;
        private static readonly Color EmptyBlockColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color FilledBlockColor = ColorLibrary.BrightGreen;
        private static readonly Color ExcessBlockColor = ColorLibrary.Red;

        private Pawn pawn;
        private Hediff_MindTransmitter mindTransmitter;

        public override float GetWidth(float maxWidth)
            => Width;

        public override bool Visible => Find.Selector.SelectedPawns.Count == 1;

        public MindTransmitterBandwidthGizmo(Pawn pawn, Hediff_MindTransmitter mindTransmitter)
        {
            this.pawn = pawn;
            this.mindTransmitter = mindTransmitter;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var backPanelRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(backPanelRect);

            var innerRect = backPanelRect.ContractedBy(6f);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            var headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, HeaderHeight);
            Widgets.Label(headerRect, LocalizeTexts.GizmoViviBandwidthHeader.Translate());

            var totalBandwidth = mindTransmitter.TotalBandWidth;
            var usedBandwidth = mindTransmitter.UsedBandwidth;

            var description = usedBandwidth.ToString("F0") + " / " + totalBandwidth.ToString("F0");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(headerRect, description);
            Text.Anchor = TextAnchor.UpperLeft;

            var maxBandWidthCount = Mathf.Max(totalBandwidth, usedBandwidth);
            var cellsArrayRect = new Rect(
                innerRect.x, 
                headerRect.yMax + 6f, 
                innerRect.width, 
                innerRect.height - headerRect.height - 6f);

            int yCellCount = 2, cellCount = 0;
            var cellSize = Mathf.FloorToInt(cellsArrayRect.height / (float)yCellCount);
            var xCellCount = Mathf.FloorToInt(cellsArrayRect.width / (float)cellSize);
            while (xCellCount * yCellCount  < maxBandWidthCount)
            {
                yCellCount++;
                cellSize = Mathf.FloorToInt(cellsArrayRect.height / (float)yCellCount);
                xCellCount = Mathf.FloorToInt(cellsArrayRect.width / (float)cellSize);

                cellCount++;
                if (cellCount >= 1000)
                {
                    Log.Error("Failed to fit bandwidth cells into gizmo rect.");
                    return new GizmoResult(GizmoState.Clear);
                }
            }

            var xPadding = (cellsArrayRect.width - (xCellCount * cellSize)) / 2f;
            var cellBorderSize = (yCellCount <= 2) ? 4 : 2;

            int index = 0;
            for (int y = 0; y < yCellCount; y++)
            {
                for (int x = 0; x < xCellCount; x++)
                {
                    index++;
                    var cellRect = new Rect(
                        cellsArrayRect.x + (float)(x * cellSize) + xPadding,
                        cellsArrayRect.y + (float)(y * cellSize),
                        cellSize,
                        cellSize).ContractedBy(2f);

                    if (index <= maxBandWidthCount)
                    {
                        if (index <= usedBandwidth)
                        {
                            Widgets.DrawRectFast(cellRect, (index <= totalBandwidth) ? FilledBlockColor : ExcessBlockColor);
                        }
                        else
                        {
                            Widgets.DrawRectFast(cellRect, EmptyBlockColor);
                        }
                    }
                }
            }

            TooltipHandler.TipRegion(backPanelRect, LocalizeTexts.GizmoViviBandwidthTooltip.Translate());
            if (Widgets.ButtonInvisible(backPanelRect))
            {
                var linked = mindTransmitter.LinkedPawns.ToList();
                if (linked.Count > 0)
                {
                    Find.WindowStack.Add(new FloatMenu(linked.Select((v) =>
                    {
                        if (pawn.MapHeld != null && v.MapHeld == pawn.MapHeld)
                        {
                            return new FloatMenuOption(v.Name.ToStringShort, () =>
                            {
                                Find.Selector.ClearSelection();
                                Find.Selector.Select(v);
                            },
                            v,
                            v.def.uiIconColor);
                        }
                        else
                        {
                            return new FloatMenuOption(v.Name.ToStringShort, null, v, v.def.uiIconColor);
                        }
                    }).ToList()));
                }
            }

            return new GizmoResult(GizmoState.Clear);
        }
    }
}
