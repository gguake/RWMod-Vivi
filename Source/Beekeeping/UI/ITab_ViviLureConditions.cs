using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    // 유인통 진행 상태와 방문·둥지 자리 조건. 조건마다 충족 여부(바닐라 체크·엑스 아이콘)와 점수(꽃·구조물·마나·둥지 점수)만 보여 주고
    // 측정값은 숨긴다(무엇을 보는지는 툴팁이 수치 없이 알려 준다, 사용자 결정). 조건 줄은 유인통이 판정과 같은 계산으로 만든다(Building_ViviLure.ConditionRows).
    // 줄 모양은 마법 식물 탭(ITab_ArcanePlant)을 따른다.
    public class ITab_ViviLureConditions : ITab
    {
        // 닫기 단추 자리(바닐라 ITab_Pawn_Gear처럼 위를 비운다)
        private const float TopMargin = 20f;
        private const float RowGap = 1f;
        private const float RowPadding = 6f;
        private const float RowHeight = 22f;
        private const float IconSize = 18f;
        private const float IconGap = 6f;
        private const float ValueColumnWidth = 190f;
        private const float MinHeight = 200f;
        // 탭은 정보창 위로 열리므로 화면 높이에서 정보창·하단 단추 줄 몫을 빼 넘치지 않게 한다(넘치면 스크롤).
        private const float ScreenReservedHeight = 260f;

        private static readonly Vector2 WinSize = new Vector2(480f, 520f);
        private static readonly Color UnmetValueColor = ColorLibrary.RedReadable;

        private Vector2 _scrollPosition;
        private float _contentHeight = WinSize.y;

        protected Building_ViviLure SelLure => SelThing as Building_ViviLure;

        public override bool IsVisible => SelThing is Building_ViviLure;

        public ITab_ViviLureConditions()
        {
            size = WinSize;
            labelKey = LocalizeString_ITab.VV_ITab_ViviLure_TabLabel;
        }

        // 내용 높이에 맞춘다(앞 프레임에서 잰 높이).
        protected override void UpdateSize()
        {
            base.UpdateSize();
            var maxHeight = Mathf.Max(MinHeight, UI.screenHeight - ScreenReservedHeight);
            size.y = Mathf.Clamp(_contentHeight + TopMargin + 20f, MinHeight, maxHeight);
        }

        protected override void FillTab()
        {
            var lure = SelLure;
            if (lure == null) { return; }

            var rows = lure.ConditionRows;
            var outRect = new Rect(0f, TopMargin, size.x, size.y - TopMargin).ContractedBy(10f);
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(_contentHeight, outRect.height));

            Widgets.BeginScrollView(outRect, ref _scrollPosition, viewRect);

            using (new TextBlock(GameFont.Small, TextAnchor.UpperLeft))
            {
                var width = viewRect.width;
                var curY = 0f;

                ITab_ArcanePlant.DrawSectionTitle(width, ref curY, LocalizeString_ITab.VV_ITab_ViviLure_SectionStatus.Translate());
                var alternate = false;
                DrawTextRow(width, ref curY, lure.PhaseStatus ?? LocalizeString_ITab.VV_ITab_ViviLure_Idle.Translate(), ref alternate);

                DrawConditionSection(width, ref curY, rows, ViviLureConditionSection.Visit, LocalizeString_ITab.VV_ITab_ViviLure_SectionVisit.Translate());
                DrawConditionSection(width, ref curY, rows, ViviLureConditionSection.Site, LocalizeString_ITab.VV_ITab_ViviLure_SectionSite.Translate());

                _contentHeight = curY;
            }

            Widgets.EndScrollView();
        }

        private void DrawConditionSection(float width, ref float curY, List<ViviLureConditionRow> rows, ViviLureConditionSection section, string title)
        {
            ITab_ArcanePlant.DrawSectionTitle(width, ref curY, title);

            var alternate = false;
            for (int i = 0; i < rows.Count; ++i)
            {
                if (rows[i].section == section)
                {
                    DrawConditionRow(width, ref curY, rows[i], ref alternate);
                }
            }
        }

        private void DrawTextRow(float width, ref float curY, string text, ref bool alternate)
        {
            var textWidth = width - RowPadding * 2f;
            var height = Mathf.Max(Text.CalcHeight(text, textWidth) + 4f, RowHeight);

            var rowRect = new Rect(0f, curY, width, height);
            if (alternate)
            {
                Widgets.DrawLightHighlight(rowRect);
            }

            Widgets.Label(new Rect(RowPadding, curY + 2f, textWidth, height - 4f), text);

            curY += height + RowGap;
            alternate = !alternate;
        }

        // 요건이면 바닐라 체크(충족)·엑스(미충족) 아이콘, 참고 값(환경 마나·둥지 점수)이면 아이콘 없이 그린다.
        private void DrawConditionRow(float width, ref float curY, ViviLureConditionRow row, ref bool alternate)
        {
            var labelX = RowPadding + IconSize + IconGap;
            var labelWidth = width - labelX - ValueColumnWidth - RowPadding;
            var textHeight = Text.CalcHeight(row.label, labelWidth);
            if (!row.value.NullOrEmpty())
            {
                textHeight = Mathf.Max(textHeight, Text.CalcHeight(row.value, ValueColumnWidth));
            }
            var height = Mathf.Max(textHeight + 4f, RowHeight);

            var rowRect = new Rect(0f, curY, width, height);
            if (alternate)
            {
                Widgets.DrawLightHighlight(rowRect);
            }

            if (row.state != ViviLureConditionState.Info)
            {
                var iconRect = new Rect(RowPadding, curY + (height - IconSize) / 2f, IconSize, IconSize);
                GUI.DrawTexture(iconRect, row.state == ViviLureConditionState.Met ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
            }

            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.Label(new Rect(labelX, curY, labelWidth, height), row.label);
            }

            if (!row.value.NullOrEmpty())
            {
                using (new TextBlock(TextAnchor.MiddleRight, row.state == ViviLureConditionState.Unmet ? UnmetValueColor : Color.white))
                {
                    Widgets.Label(new Rect(width - ValueColumnWidth - RowPadding, curY, ValueColumnWidth, height), row.value);
                }
            }

            if (!row.tooltip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rowRect, row.tooltip);
            }

            curY += height + RowGap;
            alternate = !alternate;
        }
    }
}
