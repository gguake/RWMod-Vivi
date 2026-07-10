using UnityEngine;
using Verse;

namespace VVRace
{
    public class PerfumeGizmo : Gizmo
    {
        private const float Width = 180f;
        private static readonly Color SlotColor = new Color(0.16f, 0.13f, 0.18f, 0.9f);
        private readonly CompPerfumeBottle comp;

        public PerfumeGizmo(CompPerfumeBottle comp)
        {
            this.comp = comp;
            Order = -99f;
        }

        public override bool Visible => comp != null && Find.Selector.NumSelected == 1;

        public override float GetWidth(float maxWidth)
        {
            return Width;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(outerRect);

            var innerRect = outerRect.ContractedBy(6f);
            using (new TextBlock(GameFont.Tiny, TextAnchor.UpperLeft))
            {
                Widgets.LabelEllipses(new Rect(innerRect.x, innerRect.y, innerRect.width, 18f), comp.parent.LabelCap);
                Widgets.LabelEllipses(new Rect(innerRect.x, innerRect.y + 17f, innerRect.width, 18f), comp.GetStatusText());
            }

            const float slotSize = 27f;
            const float slotGap = 4f;
            var totalWidth = comp.Props.maxIngredients * slotSize + (comp.Props.maxIngredients - 1) * slotGap;
            var slotX = innerRect.x + (innerRect.width - totalWidth) / 2f;
            var slotY = innerRect.y + 37f;

            for (var index = 0; index < comp.Props.maxIngredients; index++)
            {
                var slotRect = new Rect(slotX + index * (slotSize + slotGap), slotY, slotSize, slotSize);
                Widgets.DrawBoxSolid(slotRect, SlotColor);
                Widgets.DrawBox(slotRect);

                if (index < comp.Ingredients.Count)
                {
                    var ingredient = comp.Ingredients[index];
                    Widgets.DrawTextureFitted(slotRect.ContractedBy(2f), ingredient.uiIcon, 1f);
                    TooltipHandler.TipRegion(slotRect, ingredient.LabelCap);
                }
            }

            TooltipHandler.TipRegion(outerRect, comp.GetTooltip());
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
