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

        private Pawn _pawn;

        public override float GetWidth(float maxWidth)
            => Width;

        public override bool Visible => _pawn != null && _pawn.IsRoyalVivi() && Find.Selector.SelectedPawns.Count == 1;

        public EggProgressGizmo(Pawn vivi)
        {
            _pawn = vivi;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var eggSpawnerComp = _pawn.TryGetComp<CompViviEggLayer>();

            var backPanelRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(backPanelRect);

            var innerRect = backPanelRect.ContractedBy(6f);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            var headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, HeaderHeight);
            Widgets.Label(headerRect, LocalizeString_Gizmo.VV_Gizmo_ViviEggProgressHeader.Translate());

            Text.Font = GameFont.Tiny;
            var descriptionRect = new Rect(innerRect.x, headerRect.yMax + 2f, innerRect.width, HeaderHeight);
            var description = eggSpawnerComp.eggProgress.ToStringPercent("F1");
            Widgets.Label(descriptionRect, description);

            Text.Anchor = TextAnchor.UpperRight;
            var descriptionPerDay = $"(+{eggSpawnerComp.EggProgressPerDays.ToStringPercent()}/day)";
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
            frontRect.width = frontRect.width * eggSpawnerComp.eggProgress;

            Widgets.DrawBoxSolid(frontRect, FilledBlockColor);

            TooltipHandler.TipRegion(backPanelRect, LocalizeString_Gizmo.VV_Gizmo_ViviEggProgressTooltip.Translate());
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
