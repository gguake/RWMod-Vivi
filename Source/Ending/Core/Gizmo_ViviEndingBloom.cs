using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class Gizmo_ViviEndingBloom : Gizmo
    {
        private const float GizmoWidth = 180f;
        private const float Padding = 6f;
        private const float LabelHeight = 18f;
        private const float BarHeight = 24f;
        private const float Spacing = 2f;

        private static readonly Texture2D SiegeBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.95f, 0.76f, 0.3f));
        private static readonly Texture2D CalmBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.68f, 0.66f, 0.85f));
        private static readonly Texture2D BlockedBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.25f, 0.55f));
        private static readonly Texture2D CoronationBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.88f, 0.45f));
        private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.12f, 0.12f, 0.14f));

        private readonly ViviEndingQuest _quest;

        public Gizmo_ViviEndingBloom(ViviEndingQuest quest)
        {
            _quest = quest;
            Order = -95f;
        }

        public override float GetWidth(float maxWidth)
        {
            return Mathf.Min(GizmoWidth, maxWidth);
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(rect);

            var inner = rect.ContractedBy(Padding);
            var progress = _quest.BloomProgress;

            // 진행 상황은 응축 %만 알려 준다. 단계 설명·남은 시간 같은 안내는 표시하지 않는다.
            Texture2D barTex;
            switch (_quest.Stage)
            {
                case ViviEndingStage.Calm:
                    barTex = CalmBarTex;
                    break;

                case ViviEndingStage.Boss:
                    barTex = BlockedBarTex;
                    break;

                case ViviEndingStage.Coronation:
                    barTex = CoronationBarTex;
                    break;

                default:
                    barTex = SiegeBarTex;
                    break;
            }

            var labelRect = new Rect(inner.x, inner.y, inner.width, LabelHeight);
            using (new TextBlock(GameFont.Tiny, TextAnchor.UpperLeft))
            {
                Widgets.Label(labelRect, LocalizeString_EndingCore.VV_Gizmo_EndingBloom.Translate().Resolve());
            }

            var barRect = new Rect(inner.x, labelRect.yMax + Spacing, inner.width, Mathf.Max(BarHeight, inner.yMax - labelRect.yMax - Spacing));
            Widgets.FillableBar(barRect, progress, barTex, EmptyBarTex, doBorder: false);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleCenter))
            {
                Widgets.Label(barRect, progress.ToStringPercent());
            }

            TooltipHandler.TipRegion(rect, LocalizeString_EndingCore.VV_Gizmo_EndingBloomDesc.Translate().Resolve());
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
