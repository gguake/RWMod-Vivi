using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class EggProgressGizmo : Gizmo
    {
        private const float Width = 136f;
        private const int HeaderHeight = 20;
        private static readonly Color EmptyBlockColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color FilledBlockColor = Color.grey;

        private Vivi _vivi;

        public override float GetWidth(float maxWidth)
            => Width;

        public override bool Visible => _vivi != null && _vivi.IsRoyal && Find.Selector.SelectedPawns.Count == 1;

        public EggProgressGizmo(Vivi vivi)
        {
            _vivi = vivi;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var backPanelRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(backPanelRect);

            var innerRect = backPanelRect.ContractedBy(6f);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            var headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, HeaderHeight);
            Widgets.Label(headerRect, LocalizeTexts.GizmoViviEggProgressHeader.Translate());

            Text.Font = GameFont.Tiny;
            var descriptionRect = new Rect(innerRect.x, headerRect.yMax + 2f, innerRect.width, HeaderHeight);
            var description = _vivi.EggProgress.ToStringPercent("F1");
            Widgets.Label(descriptionRect, description);

            Text.Anchor = TextAnchor.UpperRight;
            var descriptionPerDay = $"(+{_vivi.EggProgressPerDays.ToStringPercent()}/day)";
            Widgets.Label(descriptionRect, descriptionPerDay);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            var backRect = new Rect(
                innerRect.x,
                descriptionRect.yMax + 2f, 
                innerRect.width, 
                innerRect.height - headerRect.height - descriptionRect.height - 4f);

            Widgets.DrawBoxSolid(backRect, EmptyBlockColor);

            var frontRect = backRect.ContractedBy(3f);
            frontRect.width = frontRect.width * _vivi.EggProgress;

            Widgets.DrawBoxSolid(frontRect, FilledBlockColor);

            TooltipHandler.TipRegion(backPanelRect, LocalizeTexts.GizmoViviEggProgressTooltip.Translate());
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
