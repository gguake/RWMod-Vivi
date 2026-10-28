using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 앵커 영원꽃의 6단계 개화 응축 진행 막대. 공성/평화 단계 이름과 백분율을 보여주고, 보스 단계에는 "가로막힘",
    // 토벌 뒤에는 "대관식 대기"로 표시한다.
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

            Texture2D barTex;
            string status;
            string stageDesc;
            switch (_quest.Stage)
            {
                case ViviEndingStage.Calm:
                    barTex = CalmBarTex;
                    status = LocalizeString_EndingCore.VV_Gizmo_EndingBloomCalm.Translate();
                    stageDesc = _quest.IsBossDeferred
                        ? LocalizeString_EndingCore.VV_Inspector_EndingCalmDeferred.Translate().Resolve()
                        : LocalizeString_EndingCore.VV_Gizmo_EndingBloomDescCalm.Translate(_quest.CalmTicksLeft.ToStringTicksToPeriod().Named("TIME")).Resolve();
                    break;

                case ViviEndingStage.Boss:
                    barTex = BlockedBarTex;
                    status = LocalizeString_EndingCore.VV_Gizmo_EndingBloomBlocked.Translate();
                    stageDesc = LocalizeString_EndingCore.VV_Gizmo_EndingBloomDescBlocked.Translate();
                    break;

                case ViviEndingStage.Coronation:
                    barTex = CoronationBarTex;
                    status = LocalizeString_EndingCore.VV_Gizmo_EndingBloomCoronation.Translate();
                    stageDesc = LocalizeString_EndingCore.VV_Gizmo_EndingBloomDescCoronation.Translate();
                    break;

                default:
                    barTex = SiegeBarTex;
                    status = LocalizeString_EndingCore.VV_Gizmo_EndingBloomSiege.Translate();
                    stageDesc = LocalizeString_EndingCore.VV_Gizmo_EndingBloomDescSiege.Translate();
                    break;
            }

            var labelRect = new Rect(inner.x, inner.y, inner.width, LabelHeight);
            using (new TextBlock(GameFont.Tiny, TextAnchor.UpperLeft))
            {
                Widgets.Label(labelRect, LocalizeString_EndingCore.VV_Gizmo_EndingBloom.Translate().Resolve());
            }

            var barRect = new Rect(inner.x, labelRect.yMax + Spacing, inner.width, BarHeight);
            Widgets.FillableBar(barRect, progress, barTex, EmptyBarTex, doBorder: false);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleCenter))
            {
                Widgets.Label(barRect, progress.ToStringPercent());
            }

            var statusRect = new Rect(inner.x, barRect.yMax + Spacing, inner.width, Mathf.Max(0f, inner.yMax - barRect.yMax - Spacing));
            using (new TextBlock(GameFont.Tiny, TextAnchor.UpperLeft))
            {
                Widgets.Label(statusRect, status);
            }

            TooltipHandler.TipRegion(rect, LocalizeString_EndingCore.VV_Gizmo_EndingBloomDesc.Translate().Resolve() + "\n\n" + stageDesc);
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
