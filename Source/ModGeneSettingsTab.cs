using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VVRace
{
    public class ModGeneSettingsTab
    {
        private const float SectionGap = 8f;
        private const float ColumnGap = 16f;
        private const float HeaderHeight = 30f;
        private const float MoveAllButtonWidth = 140f;
        private const float SearchWidth = 260f;
        private const float RowHeight = 32f;
        private const float RowPadding = 4f;
        private const float IconSize = 28f;
        private const float ArrowSize = 20f;

        private static readonly CachedTexture XenogeneBackgroundTex = new CachedTexture("UI/Icons/Genes/GeneBackground_Xenogene");
        private static readonly CachedTexture ArchiteGeneBackgroundTex = new CachedTexture("UI/Icons/Genes/GeneBackground_ArchiteGene");

        private readonly QuickSearchWidget _searchWidget = new QuickSearchWidget();
        private readonly List<GeneDef> _disabledGenes = new List<GeneDef>();
        private readonly List<GeneDef> _enabledGenes = new List<GeneDef>();

        private List<GeneDef> _candidates;
        private Window _candidatesWindow;
        private Vector2 _disabledScrollPosition;
        private Vector2 _enabledScrollPosition;

        public void Draw(Rect rect, VVRaceModSettings settings)
        {
            settings.ResolvePendingModGenes();
            RefreshCandidates();

            var curY = rect.y;
            using (new TextBlock(GameFont.Small, ColoredText.SubtleGrayColor))
            {
                var description = LocalizeString_Etc.VV_ModSettings_ModGenesDesc.Translate();
                var descriptionHeight = Text.CalcHeight(description, rect.width);
                Widgets.Label(new Rect(rect.x, curY, rect.width, descriptionHeight), description);
                curY += descriptionHeight + SectionGap;
            }

            if (_candidates.Count == 0)
            {
                using (new TextBlock(TextAnchor.MiddleCenter))
                {
                    Widgets.Label(
                        new Rect(rect.x, curY, rect.width, rect.yMax - curY),
                        LocalizeString_Etc.VV_ModSettings_NoModGenes.Translate());
                }
                return;
            }

            _searchWidget.OnGUI(new Rect(rect.x, curY, Mathf.Min(SearchWidth, rect.width), QuickSearchWidget.WidgetHeight));
            curY += QuickSearchWidget.WidgetHeight + SectionGap;

            var disabledCount = 0;
            _disabledGenes.Clear();
            _enabledGenes.Clear();
            foreach (var def in _candidates)
            {
                var enabled = settings.IsModGeneEnabled(def);
                if (!enabled)
                {
                    disabledCount++;
                }

                if (MatchesFilter(def))
                {
                    (enabled ? _enabledGenes : _disabledGenes).Add(def);
                }
            }

            _searchWidget.noResultsMatched = _disabledGenes.Count == 0 && _enabledGenes.Count == 0;

            var columnWidth = (rect.width - ColumnGap) / 2f;
            var columnRect = new Rect(rect.x, curY, columnWidth, rect.yMax - curY);
            DrawColumn(columnRect, settings, _disabledGenes, disabledCount, false, ref _disabledScrollPosition);

            columnRect.x += columnWidth + ColumnGap;
            DrawColumn(columnRect, settings, _enabledGenes, _candidates.Count - disabledCount, true, ref _enabledScrollPosition);
        }

        // 설정 창을 새로 열 때마다 다시 계산한다. (언어 변경 등으로 def가 다시 로드될 수 있음)
        private void RefreshCandidates()
        {
            var window = Find.WindowStack.currentlyDrawnWindow;
            if (_candidates != null && window == _candidatesWindow) { return; }

            _candidatesWindow = window;
            _candidates = ViviUtility.GetModGeneCandidates();
            _searchWidget.Reset();
            _disabledScrollPosition = Vector2.zero;
            _enabledScrollPosition = Vector2.zero;
        }

        private bool MatchesFilter(GeneDef def)
        {
            var filter = _searchWidget.filter;
            return filter.Matches(GetGeneLabel(def)) || (def.modContentPack != null && filter.Matches(def.modContentPack.Name));
        }

        private void DrawColumn(Rect rect, VVRaceModSettings settings, List<GeneDef> genes, int totalCount, bool enabled, ref Vector2 scrollPosition)
        {
            var headerRect = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
            var buttonRect = new Rect(headerRect.xMax - MoveAllButtonWidth, headerRect.y + 2f, MoveAllButtonWidth, HeaderHeight - 4f);

            var countText = _searchWidget.filter.Active ? $"{genes.Count}/{totalCount}" : totalCount.ToString();
            var title = enabled ?
                LocalizeString_Etc.VV_ModSettings_EnabledModGenes.Translate(countText) :
                LocalizeString_Etc.VV_ModSettings_DisabledModGenes.Translate(countText);
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                Widgets.LabelEllipses(new Rect(headerRect.x, headerRect.y, buttonRect.x - headerRect.x - RowPadding, HeaderHeight), title);
            }

            var buttonLabel = enabled ?
                LocalizeString_Etc.VV_ModSettings_DisableAllModGenes.Translate() :
                LocalizeString_Etc.VV_ModSettings_EnableAllModGenes.Translate();
            var canMoveAll = genes.Count > 0;
            using (new TextBlock(canMoveAll ? Color.white : Color.gray))
            {
                if (Widgets.ButtonText(buttonRect, buttonLabel, doMouseoverSound: canMoveAll, active: canMoveAll))
                {
                    foreach (var def in genes)
                    {
                        settings.SetModGeneEnabled(def, !enabled);
                    }

                    PlayMoveSound(!enabled);
                }
            }

            TooltipHandler.TipRegion(buttonRect, enabled ?
                LocalizeString_Etc.VV_ModSettings_DisableAllModGenesDesc.Translate() :
                LocalizeString_Etc.VV_ModSettings_EnableAllModGenesDesc.Translate());

            var listRect = new Rect(rect.x, headerRect.yMax + RowPadding, rect.width, rect.yMax - headerRect.yMax - RowPadding);
            Widgets.DrawMenuSection(listRect);

            var outRect = listRect.ContractedBy(RowPadding);
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, genes.Count * RowHeight);
            scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f, Mathf.Max(0f, viewRect.height - outRect.height));

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            // 모드 유전자가 수백 개일 수 있으므로 보이는 행만 그린다.
            var firstIndex = Mathf.Max(0, Mathf.FloorToInt(scrollPosition.y / RowHeight));
            var lastIndex = Mathf.Min(genes.Count - 1, Mathf.FloorToInt((scrollPosition.y + outRect.height) / RowHeight));
            for (var i = firstIndex; i <= lastIndex; i++)
            {
                var def = genes[i];
                if (DrawGeneRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight), def, enabled, i % 2 == 1))
                {
                    settings.SetModGeneEnabled(def, !enabled);
                    PlayMoveSound(!enabled);
                }
            }

            Widgets.EndScrollView();

            if (genes.Count == 0)
            {
                using (new TextBlock(TextAnchor.MiddleCenter, ColoredText.SubtleGrayColor))
                {
                    Widgets.Label(outRect, LocalizeString_Etc.VV_ModSettings_ModGeneListEmpty.Translate());
                }
            }
        }

        private bool DrawGeneRow(Rect rowRect, GeneDef def, bool enabled, bool alternate)
        {
            if (alternate)
            {
                Widgets.DrawLightHighlight(rowRect);
            }

            Widgets.DrawHighlightIfMouseover(rowRect);

            // 화살표는 옮겨질 쪽(활성 목록은 왼쪽, 비활성 목록은 오른쪽)에 그린다.
            var arrowRect = new Rect(
                enabled ? rowRect.x + RowPadding : rowRect.xMax - RowPadding - ArrowSize,
                rowRect.y + (rowRect.height - ArrowSize) / 2f,
                ArrowSize,
                ArrowSize);
            GUI.DrawTexture(arrowRect, enabled ? TexUI.ArrowTexLeft : TexUI.ArrowTexRight);

            var contentRect = rowRect;
            if (enabled)
            {
                contentRect.xMin = arrowRect.xMax + RowPadding;
            }
            else
            {
                contentRect.xMax = arrowRect.xMin - RowPadding;
            }

            var iconRect = new Rect(contentRect.x + RowPadding, rowRect.y + (rowRect.height - IconSize) / 2f, IconSize, IconSize);
            DrawGeneIcon(iconRect, def);

            var textRect = new Rect(iconRect.xMax + 6f, rowRect.y, contentRect.xMax - iconRect.xMax - 6f - RowPadding, rowRect.height);
            var modName = def.modContentPack?.Name ?? string.Empty;
            using (new TextBlock(GameFont.Tiny, TextAnchor.MiddleRight, false, ColoredText.SubtleGrayColor))
            {
                // LabelEllipses는 폭에서 여백을 더 빼고 자르므로 글자 폭에 맞춘 칸에는 Truncate를 쓴다.
                var modNameWidth = Mathf.Min(Text.CalcSize(modName).x, textRect.width * 0.4f);
                Widgets.Label(new Rect(textRect.xMax - modNameWidth, textRect.y, modNameWidth, textRect.height), modName.Truncate(modNameWidth));
                textRect.xMax -= modNameWidth + 6f;
            }

            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            {
                Widgets.LabelEllipses(textRect, GetGeneLabel(def));
            }

            if (Mouse.IsOver(rowRect))
            {
                TooltipHandler.TipRegion(rowRect, new TipSignal(GetGeneTooltip(def, enabled), def.shortHash + (enabled ? 100000 : 0)));
            }

            return Widgets.ButtonInvisible(rowRect);
        }

        private static void DrawGeneIcon(Rect rect, GeneDef def)
        {
            var background = def.biostatArc != 0 ? ArchiteGeneBackgroundTex : XenogeneBackgroundTex;
            GUI.DrawTexture(rect, background.Texture);
            Widgets.DefIcon(rect, def, null, 0.9f, null, drawPlaceholder: false, color: def.IconColor);
        }

        // 라벨이 없는 모드 유전자도 있을 수 있으므로 defName으로 대신한다.
        private static string GetGeneLabel(GeneDef def)
        {
            return def.label.NullOrEmpty() ? def.defName : def.LabelCap.Resolve();
        }

        private static string GetGeneTooltip(GeneDef def, bool enabled)
        {
            var tooltip = GetGeneLabel(def).Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + def.DescriptionFull;
            if (def.modContentPack != null)
            {
                tooltip += "\n\n" + LocalizeString_Etc.VV_ModSettings_ModGeneSource.Translate(def.modContentPack.Name).Colorize(ColoredText.SubtleGrayColor);
            }

            var clickText = enabled ?
                LocalizeString_Etc.VV_ModSettings_ModGeneClickToDisable.Translate() :
                LocalizeString_Etc.VV_ModSettings_ModGeneClickToEnable.Translate();
            return tooltip + "\n\n" + clickText.Colorize(ColoredText.SubtleGrayColor);
        }

        private static void PlayMoveSound(bool enabled)
        {
            (enabled ? SoundDefOf.Tick_High : SoundDefOf.Tick_Low).PlayOneShotOnCamera();
        }
    }
}
