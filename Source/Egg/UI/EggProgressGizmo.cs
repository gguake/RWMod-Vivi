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

            var headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, HeaderHeight);
            using (new TextBlock(GameFont.Tiny))
            {
                Widgets.Label(headerRect, LocalizeString_Gizmo.VV_Gizmo_ViviEggProgressHeader.Translate());
            }

            var descriptionRect = new Rect(innerRect.x, headerRect.yMax + 2f, innerRect.width, HeaderHeight);
            using (new TextBlock(GameFont.Tiny))
            {
                var description = eggSpawnerComp.eggProgress.ToStringPercent("F1");
                Widgets.Label(descriptionRect, description);

                using (new TextBlock(TextAnchor.UpperRight))
                {
                    var descriptionPerDay = $"(+{eggSpawnerComp.EggProgressPerDays.ToStringPercent()}/day)";
                    Widgets.Label(descriptionRect, descriptionPerDay);
                }
            }

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
