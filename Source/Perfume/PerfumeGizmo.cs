using UnityEngine;
using Verse;

namespace VVRace
{
    public class PerfumeGizmo : Gizmo
    {
        private static readonly Color SlotColor = new Color(0.16f, 0.13f, 0.18f, 0.9f);

        private const float Width = 140f;
        private const float HeaderHeight = 32f;
        private const float SlotSize = 27f;
        private const float SlotGap = 4f;
        private const int LayoutHash = 126793481;

        private readonly CompPerfumeBottle _comp;

        public PerfumeGizmo(CompPerfumeBottle comp)
        {
            this._comp = comp;
            Order = -99f;
        }

        public override bool Visible => _comp != null && _comp != null && _comp.parent is Pawn pawn && pawn.IsColonistPlayerControlled && Find.Selector.NumSelected == 1;

        public override float GetWidth(float maxWidth)
        {
            return Width;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(outerRect);

            var layout = new RectDivider(
                outerRect.ContractedBy(6f),
                LayoutHash,
                margin: new Vector2(SlotGap, SlotGap));
            var header = layout.NewRow(HeaderHeight);
            var statusRect = header.Rect;
            using (new TextBlock(GameFont.Tiny, TextAnchor.UpperLeft))
            {
                Widgets.LabelEllipses(statusRect, _comp.GetStatusText());
            }

            var slotRow = layout.NewRow(SlotSize, marginOverride: 0f);
            var totalWidth = _comp.Props.maxIngredients * SlotSize + (_comp.Props.maxIngredients - 1) * SlotGap;
            var centeredSlots = slotRow;
            centeredSlots.NewCol((slotRow.Rect.width - totalWidth) / 2f, marginOverride: 0f);
            var slots = centeredSlots.NewCol(totalWidth, marginOverride: 0f);
            var slotLayout = new RectDivider(
                slots.Rect,
                LayoutHash ^ 7919,
                margin: new Vector2(SlotGap, 0f));

            for (var index = 0; index < _comp.Props.maxIngredients; index++)
            {
                var slotRect = slotLayout.NewCol(
                    SlotSize,
                    marginOverride: index == _comp.Props.maxIngredients - 1 ? 0f : SlotGap).Rect;
                Widgets.DrawBoxSolid(slotRect, SlotColor);
                Widgets.DrawBox(slotRect);

                if (index < _comp.Ingredients.Count)
                {
                    var ingredient = _comp.Ingredients[index];
                    Widgets.DrawTextureFitted(slotRect.ContractedBy(2f), ingredient.uiIcon, 1f);
                    TooltipHandler.TipRegion(slotRect, ingredient.LabelCap);
                }
            }

            TooltipHandler.TipRegion(outerRect, _comp.GetTooltip());
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
