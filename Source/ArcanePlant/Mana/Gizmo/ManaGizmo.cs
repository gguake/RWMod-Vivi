using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaGizmo : Gizmo
    {
        private const float Width = 200f;
        private static readonly Color EmptyBlockColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color FilledBlockColor = Color.grey;

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

            var mainRect = new RectDivider(backPanelRect.ContractedBy(6f), 686495832, margin: new Vector2(4f, 2f));
            var iconRect = mainRect.NewCol(64f);
            {
                var thingIcon = _comp.parent.UIIconOverride ?? _comp.parent.def.GetUIIconForStuff(_comp.parent.Stuff);
                if (thingIcon != null)
                {
                    var r = new Rect(0f, 0f, 64f, 64f);
                    r.center = iconRect.Rect.center;
                    Widgets.DrawTextureFitted(r, thingIcon, 1f, );
                }
            }
            var headerRow = mainRect.NewRow(40f);
            {
                using (new TextBlock(GameFont.Small, TextAnchor.UpperLeft))
                {
                    Widgets.Label(headerRow, LocalizeString_Gizmo.VV_Gizmo_ManaStorageHeader.Translate());
                }

                using (new TextBlock(GameFont.Tiny, TextAnchor.LowerLeft))
                {
                    var description = $"{_comp.Stored.ToString("F0")}/{_comp.Props.manaCapacity}";
                    Widgets.Label(headerRow, description);
                }
            }

            var progressRect = mainRect.Rect;
            Widgets.DrawBoxSolid(progressRect, EmptyBlockColor);

            var frontRect = progressRect.ContractedBy(3f);
            frontRect.width = frontRect.width * _comp.StoredPct;

            Widgets.DrawBoxSolid(frontRect, FilledBlockColor);

            TooltipHandler.TipRegion(
                backPanelRect, 
                LocalizeString_Gizmo.VV_Gizmo_ManaStorageTooltip.Translate(
                    _comp.ManaExternalChangeByDay));

            if (Mouse.IsOver(backPanelRect))
            {
                Find.CurrentMap?.GetComponent<EnvironmentManaGrid>()?.MarkForDrawOverlay();
            }

            return new GizmoResult(GizmoState.Clear);
        }
    }
}
