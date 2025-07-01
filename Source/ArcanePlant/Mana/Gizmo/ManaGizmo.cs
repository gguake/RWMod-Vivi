using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaGizmo : Gizmo
    {
        private const float Width = 160f;
        private static readonly Color EmptyBlockColor = new Color(0.2f, 0.2f, 0.24f, 1f);

        private CompMana _comp;

        public override float GetWidth(float maxWidth) => Width;

        public override bool Visible => _comp != null && Find.Selector.NumSelected == 1;

        public ManaGizmo(CompMana compMana)
        {
            _comp = compMana;

            Order = -100;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var backPanelRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(backPanelRect);

            var mainRect = new RectDivider(backPanelRect.ContractedBy(5f), 686495832, margin: new Vector2(4f, 1f));

            var upperRect = mainRect.NewRow(38f);
            {
                var labelRect = upperRect.NewRow(19f, marginOverride: 0f);
                using (new TextBlock(GameFont.Tiny, TextAnchor.UpperLeft))
                {
                    Widgets.LabelEllipses(upperRect, _comp.parent.LabelShortCap);
                }
                using (new TextBlock(GameFont.Tiny, TextAnchor.LowerLeft))
                {
                    Widgets.Label(labelRect, LocalizeString_Gizmo.VV_Gizmo_ManaStorageHeader.Translate());
                }
            }

            var progressRect = mainRect.Rect.ContractedBy(2f);
            var progressBarRect = progressRect;
            progressBarRect.width = progressRect.width * _comp.StoredPct;

            Widgets.DrawBoxSolid(progressBarRect, EmptyBlockColor);

            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleCenter))
            {
                var progressText = $"{_comp.Stored.ToString("F0")}/{_comp.Props.manaCapacity}";
                Widgets.Label(progressRect, progressText);
            }

            TooltipHandler.TipRegion(
                backPanelRect, 
                LocalizeString_Gizmo.VV_Gizmo_ManaStorageTooltip.Translate(
                    _comp.parent.LabelCap.Colorize(Color.yellow).Named("THING"),
                    (-_comp.ManaExternalChangeByDay).ToString().Colorize(Color.yellow).Named("DAILYMANA")));

            if (Mouse.IsOver(backPanelRect))
            {
                Find.CurrentMap?.GetComponent<EnvironmentManaGrid>()?.MarkForDrawOverlay();
            }

            return new GizmoResult(GizmoState.Clear);
        }
    }
}
