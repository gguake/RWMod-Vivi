using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class Gizmo_ViviFairyStatus : Gizmo
    {
        private const float CellSize = 13f;
        private const float CellGap = 4f;
        private const float Padding = 8f;
        private const float LabelHeight = 20f;

        private static readonly Color IdleColor = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color ActionColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Color EmptyColor = new Color(0.25f, 0.25f, 0.28f, 1f);

        private readonly CompViviFairyController _comp;

        public Gizmo_ViviFairyStatus(CompViviFairyController comp)
        {
            _comp = comp;
            Order = -90f;
        }

        public override bool Visible => _comp != null && _comp.MaterializedCount > 0 && Find.Selector.NumSelected == 1;

        private int SlotCount => Mathf.Max(_comp.FairyPoolCount, _comp.MaterializedCount);

        public override float GetWidth(float maxWidth)
        {
            int slots = Mathf.Max(1, SlotCount);
            float w = Padding * 2f + slots * CellSize + (slots - 1) * CellGap;
            return Mathf.Clamp(w, 75f, maxWidth);
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(rect);

            var inner = rect.ContractedBy(Padding);

            using (new TextBlock(GameFont.Tiny, TextAnchor.UpperLeft))
            {
                Widgets.Label(new Rect(inner.x, inner.y, inner.width, LabelHeight),
                    LocalizeString_Gizmo.VV_Gizmo_FairyStatusHeader.Translate());
            }

            var ordered = _comp.ActiveFairies
                .Where(f => f != null && !f.Destroyed)
                .OrderByDescending(f => f.InAction)
                .ToList();

            float x = inner.x;
            float y = inner.y + LabelHeight + 4f;
            int slots = SlotCount;
            for (int i = 0; i < slots; i++)
            {
                var cell = new Rect(x, y, CellSize, CellSize);
                Color color;
                if (i < ordered.Count)
                {
                    color = ordered[i].InAction ? ActionColor : IdleColor;
                }
                else
                {
                    color = EmptyColor;
                }

                Widgets.DrawBoxSolid(cell, color);
                x += CellSize + CellGap;
            }

            TooltipHandler.TipRegion(rect, LocalizeString_Gizmo.VV_Gizmo_FairyStatusTooltip.Translate(
                _comp.MaterializedCount, _comp.FairyPoolCount));

            return new GizmoResult(GizmoState.Clear);
        }
    }
}
